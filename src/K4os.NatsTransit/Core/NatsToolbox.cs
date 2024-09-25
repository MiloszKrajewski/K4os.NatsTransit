using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace K4os.NatsTransit.Core;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public class NatsToolbox
{
    public static readonly Task<object?> NullCompletedTask = Task.FromResult<object?>(null);
    public static readonly byte[] EmptyPayload = [];
    
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
        INatsMessageTracer? messageTracer = null)
    {
        _loggerFactory = loggerFactory;
        _connection = connection;
        _jetStream = jetStream;
        _serializerFactory = serializerFactory;
        _exceptionSerializer =
            serializerFactory.GetExceptionSerializer() ??
            DumbExceptionSerializer.Instance;
        _eventObserver = new ObservableEvent<INotification>();
        _messageTracer = messageTracer ?? NullMessageTracer.Instance;
    }

    public ILoggerFactory LoggerFactory => _loggerFactory;
    public INatsConnection Connection => _connection;
    public INatsJSContext JetStream => _jetStream;

    public OutboundAdapter<T> Serializer<T>() => _serializerFactory.GetOutboundAdapter<T>();
    public InboundAdapter<T> Deserializer<T>() => _serializerFactory.GetInboundAdapter<T>();

    public IObservable<INotification> Events => _eventObserver;
    
    public ILogger GetLoggerFor(object component) =>
        _loggerFactory.CreateLogger(component.GetType().GetFriendlyName());
    
    public void OnEvent<TEvent>(TEvent @event)
        where TEvent: INotification =>
        _eventObserver.OnNext(@event);

    public TMessage Unpack<TPayload, TMessage>(
        Exception? error, string subject, NatsHeaders? headers, TPayload? data,
        IInboundTransformer<TPayload, TMessage> transformer)
    {
        error?.Rethrow();
        
        var errorText = headers.TryGetError();
        var exception = errorText is null ? null : _exceptionSerializer.Deserialize(errorText);
        
        exception?.Rethrow();

        var response = transformer.Transform(subject, headers, data.ThrowIfNull());
        return response;
    }

    // ReSharper disable once UnusedParameter.Local
    private string? GetKnownType<TResponse>(TResponse response) => null;

    // ReSharper disable once UnusedMethodReturnValue.Local
    private static bool TryAddHeader(ref Dictionary<string, StringValues>? headers, string key, string? value) =>
        value is not null && (headers ??= new Dictionary<string, StringValues>()).TryAdd(key, value);

    private void TryAddTrace(ref Dictionary<string, StringValues>? headers) =>
        _messageTracer.Inject(Activity.Current?.Context, ref headers);

    private ActivityContext? TryRestoreTrace(NatsHeaders? headers) =>
        _messageTracer.Extract(headers);

    public Activity? SendActivity(string activityName, bool awaitsResponse) =>
        ActivitySource.StartActivity(
            activityName,
            awaitsResponse ? ActivityKind.Client : ActivityKind.Producer);

    public Activity? ReceiveActivity(
        string activityName, ActivityContext? context, bool hasResponse) =>
        ActivitySource.StartActivity(
            activityName,
            hasResponse ? ActivityKind.Server : ActivityKind.Consumer,
            context ?? default);

    public Activity? ReceiveActivity(
        string activityName, NatsHeaders? headers, bool hasResponse) =>
        ReceiveActivity(activityName, TryRestoreTrace(headers), hasResponse);
    
    public ValueTask Publish<TMessage, TPayload>(
        CancellationToken token,
        string subject, TMessage message,
        INatsSerialize<TPayload> serializer,
        IOutboundTransformer<TMessage, TPayload> transformer)
    {
        Dictionary<string, StringValues>? headers = default;
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(message));
        TryAddTrace(ref headers);
        var payload = transformer.Transform(subject, ref headers, message);
        return _connection.Publish(token, subject, serializer, headers.ToNatsHeaders(), payload);
    }

    public ValueTask Respond(
        CancellationToken token,
        string subject, Exception exception)
    {
        var payload = _exceptionSerializer.Serialize(exception);
        Dictionary<string, StringValues>? headers = default;
        TryAddHeader(ref headers, NatsConstants.ErrorHeaderName, payload);
        TryAddTrace(ref headers);
        return _connection.Publish(token, subject, null, headers.ToNatsHeaders(), EmptyPayload);
    }
    
    public ValueTask<PubAckResponse> Request<TRequest, TPayload>(
        CancellationToken token,
        string subject, TRequest request, string replySubject,
        INatsSerialize<TPayload> serializer,
        IOutboundTransformer<TRequest, TPayload> transformer)
    {
        Dictionary<string, StringValues>? headers = default;
        TryAddHeader(ref headers, NatsConstants.ReplyToHeaderName, replySubject);
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(request));
        TryAddTrace(ref headers);
        var payload = transformer.Transform(subject, ref headers, request);
        return _jetStream.Publish(token, subject, serializer, headers.ToNatsHeaders(), payload);
    }

    public ValueTask<NatsMsg<TResponsePayload>> Query<TRequest, TRequestPayload, TResponsePayload>(
        CancellationToken token,
        string subject, TRequest request,
        INatsSerialize<TRequestPayload> serializer,
        IOutboundTransformer<TRequest, TRequestPayload> outboundTransformer,
        INatsDeserialize<TResponsePayload> deserializer,
        TimeSpan timeout)
    {
        Dictionary<string, StringValues>? headers = default;
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(request));
        TryAddTrace(ref headers);
        var payload = outboundTransformer.Transform(subject, ref headers, request);
        return _connection.Request(token, subject, serializer, deserializer, timeout, headers.ToNatsHeaders(), payload);
    }

    public ValueTask Respond<TRequest, TResponse, TPayload>(
        CancellationToken token,
        NatsMsg<TRequest> request, TResponse response,
        INatsSerialize<TPayload> serializer,
        IOutboundTransformer<TResponse, TPayload> transformer)
    {
        Dictionary<string, StringValues>? headers = default;
        TryAddHeader(ref headers, NatsConstants.KnownTypeHeaderName, GetKnownType(response));
        TryAddTrace(ref headers);
        var payload = transformer.Transform(request.Subject, ref headers, response);
        return request.Respond(token, serializer, headers.ToNatsHeaders(), payload);
    }

    public ValueTask Respond<TRequest>(
        CancellationToken token,
        NatsMsg<TRequest> request, Exception exception)
    {
        var payload = _exceptionSerializer.Serialize(exception);
        Dictionary<string, StringValues>? headers = default;
        TryAddHeader(ref headers, NatsConstants.ErrorHeaderName, payload);
        TryAddTrace(ref headers);
        return request.Respond(token, null, headers.ToNatsHeaders(), EmptyPayload);
    }
    
    public IAsyncEnumerable<NatsMsg<T>> SubscribeOne<T>(
        CancellationToken token,
        string subject,
        TimeSpan timeout,
        INatsDeserialize<T> deserializer)
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
}
