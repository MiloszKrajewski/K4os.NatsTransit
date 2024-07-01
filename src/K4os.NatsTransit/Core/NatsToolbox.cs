using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace K4os.NatsTransit.Core;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public class NatsToolbox
{
    public static ActivitySource ActivitySource { get; } = new("K4os.NatsTransit");

    private readonly ILoggerFactory _loggerFactory;
    private readonly INatsConnection _connection;
    private readonly INatsJSContext _jetStream;
    private readonly INatsSerializerFactory _serializerFactory;
    private readonly IExceptionSerializer _exceptionSerializer;
    private readonly ObservableEvent<INotification> _eventObserver;
    private readonly INatsMessageTracer _messageTracer;

    public NatsToolbox(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext jetStream,
        INatsSerializerFactory serializerFactory,
        IExceptionSerializer exceptionSerializer,
        INatsMessageTracer? messageTracer = null)
    {
        _loggerFactory = loggerFactory;
        _connection = connection;
        _jetStream = jetStream;
        _serializerFactory = serializerFactory;
        _exceptionSerializer = exceptionSerializer;
        _eventObserver = new ObservableEvent<INotification>();
        _messageTracer = messageTracer ?? NullMessageTracer.Instance;
    }

    public ILoggerFactory LoggerFactory => _loggerFactory;
    public INatsConnection Connection => _connection;
    public INatsJSContext JetStream => _jetStream;

    public INatsSerialize<T> Serializer<T>() => _serializerFactory.PayloadSerializer<T>();
    public INatsDeserialize<T> Deserializer<T>() => _serializerFactory.PayloadDeserializer<T>();

    public ILogger GetLogger(object component) =>
        _loggerFactory.CreateLogger(component.GetType().GetFriendlyName());

    public IAsyncEnumerable<NatsMsg<T>> SubscribeOne<T>(
        CancellationToken token,
        string subject,
        TimeSpan timeout,
        INatsDeserialize<T> deserializer,
        bool restoreTrace = true)
    {
        var options = new NatsSubOpts { MaxMsgs = 1, StartUpTimeout = timeout };
        return _connection.SubscribeAsync(subject, null, deserializer, options, token);
    }

    public IAsyncEnumerable<NatsMsg<T>> SubscribeMany<T>(
        CancellationToken token,
        string subject,
        INatsDeserialize<T> deserializer)
    {
        var options = default(NatsSubOpts);
        return _connection.SubscribeAsync(subject, null, deserializer, options, token);
    }

    public async Task<IAsyncEnumerable<NatsJSMsg<T>>> ConsumeMany<T>(
        CancellationToken token,
        string stream, string consumer,
        INatsDeserialize<T> deserializer)
    {
        var subscription = await _jetStream.GetConsumerAsync(stream, consumer, token);
        return subscription.ConsumeAsync(deserializer, null, token);
    }

    public ValueTask Publish<TResponse, TPayload>(
        CancellationToken token,
        string subject, TResponse response,
        INatsSerialize<TPayload> serializer,
        IOutboundAdapter<TResponse, TPayload> adapter)
    {
        NatsHeaders? headers = default;
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(response));
        TryAddTrace(ref headers);
        var payload = adapter.Adapt(subject, ref headers, response);
        var message = new NatsMsg<TPayload> {
            Subject = subject,
            Headers = headers,
            Data = payload
        };
        return _connection.PublishAsync(message, serializer, null, token);
    }

    public ValueTask Publish(
        CancellationToken token,
        string subject, Exception exception)
    {
        var payload = _exceptionSerializer.Serialize(exception);
        NatsHeaders? headers = default;
        TryAddHeader(ref headers, NatsConstants.ErrorHeaderName, payload);
        TryAddTrace(ref headers);
        var message = new NatsMsg<byte[]> {
            Subject = subject,
            Headers = headers
        };
        return _connection.PublishAsync(message, null, null, token);
    }

    public ValueTask<PubAckResponse> Request<TRequest, TPayload>(
        CancellationToken token,
        string subject, TRequest request, string replySubject,
        INatsSerialize<TPayload> serializer,
        IOutboundAdapter<TRequest, TPayload> adapter)
    {
        NatsHeaders? headers = default;
        TryAddHeader(ref headers, NatsConstants.ReplyToHeaderName, replySubject);
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(request));
        TryAddTrace(ref headers);
        var payload = adapter.Adapt(subject, ref headers, request);
        return _jetStream.PublishAsync(subject, payload, serializer, null, headers, token);
    }

    public ValueTask<NatsMsg<TResponsePayload>> Query<TRequest, TRequestPayload, TResponsePayload>(
        CancellationToken token,
        string subject, TRequest request,
        INatsSerialize<TRequestPayload> serializer,
        IOutboundAdapter<TRequest, TRequestPayload> outAdapter,
        INatsDeserialize<TResponsePayload> deserializer,
        TimeSpan timeout)
    {
        NatsHeaders? headers = default;
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(request));
        TryAddTrace(ref headers);
        var payload = outAdapter.Adapt(subject, ref headers, request);
        var message = new NatsMsg<TRequestPayload> {
            Subject = subject,
            Headers = headers,
            Data = payload,
        };
        var natsSubOpts = new NatsSubOpts { MaxMsgs = 1, StartUpTimeout = timeout };
        return _connection.RequestAsync(
            message,
            serializer, deserializer,
            null, natsSubOpts,
            token
        );
    }

    public ValueTask Respond<TRequest, TResponse, TPayload>(
        CancellationToken token,
        NatsMsg<TRequest> request, TResponse response,
        INatsSerialize<TPayload> serializer,
        IOutboundAdapter<TResponse, TPayload> adapter)
    {
        NatsHeaders? headers = default;
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(response));
        TryAddTrace(ref headers);
        var payload = adapter.Adapt(request.Subject, ref headers, response);
        var message = new NatsMsg<TPayload> {
            Headers = headers,
            Data = payload
        };
        return request.ReplyAsync(message, serializer, null, token);
    }

    public ValueTask Respond<TRequest>(
        CancellationToken token,
        NatsMsg<TRequest> request, Exception exception)
    {
        var payload = _exceptionSerializer.Serialize(exception);
        NatsHeaders? headers = default;
        TryAddHeader(ref headers, NatsConstants.ErrorHeaderName, payload);
        TryAddTrace(ref headers);
        var message = new NatsMsg<byte[]> {
            Headers = headers
        };
        return request.ReplyAsync(message, null, null, token);
    }

    public void OnEvent<TEvent>(TEvent @event)
        where TEvent: INotification =>
        _eventObserver.OnNext(@event);

    public IObservable<INotification> Events => _eventObserver;

    public TMessage Unpack<TPayload, TMessage>(
        Exception? error, string subject, NatsHeaders? headers, TPayload? data,
        IInboundAdapter<TPayload, TMessage> adapter)
    {
        error?.Rethrow();
        var errorText = headers.TryGetError();
        var exception = errorText is null ? null : _exceptionSerializer.Deserialize(errorText);
        if (exception is not null) throw exception;

        var response = adapter.Adapt(subject, headers, data.ThrowIfNull());
        return response;
    }

    // ReSharper disable once UnusedParameter.Local
    private string? GetKnownType<TResponse>(TResponse response) => null;

    // ReSharper disable once UnusedMethodReturnValue.Local
    private static bool TryAddHeader(ref NatsHeaders? headers, string key, string? value) =>
        value is not null && (headers ??= new NatsHeaders()).TryAdd(key, value);

    private void TryAddTrace(ref NatsHeaders? headers) =>
        _messageTracer.Inject(Activity.Current?.Context, ref headers);

    private ActivityContext? TryRestoreTrace(NatsHeaders? headers) =>
        _messageTracer.Extract(headers);
    
    public Activity? SendActivity(string activityName) =>
        ActivitySource.StartActivity(activityName, ActivityKind.Producer);
    
    public Activity? ReceiveActivity(string activityName, ActivityContext? context) =>
        ActivitySource.StartActivity(activityName, ActivityKind.Consumer, context ?? default);
    
    public Activity? ReceiveActivity(string activityName, NatsHeaders? headers) =>
        ReceiveActivity(activityName, TryRestoreTrace(headers));
}
