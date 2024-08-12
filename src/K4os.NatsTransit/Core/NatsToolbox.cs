using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public class NatsToolbox
{
    public static ActivitySource ActivitySource { get; } = new("K4os.NatsTransit");

    private readonly ILoggerFactory _loggerFactory;
    private readonly INatsConnection _connection;
    private readonly INatsJSContext _jetStream;
    private readonly INatsSerializerXFactory _serializerFactory;
    private readonly IExceptionSerializer _exceptionSerializer;
    private readonly ObservableEvent<INotification> _eventObserver;
    private readonly INatsMessageTracer _messageTracer;

    public NatsToolbox(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext jetStream,
        INatsSerializerXFactory serializerFactory,
        INatsMessageTracer? messageTracer = null)
    {
        _loggerFactory = loggerFactory;
        _connection = connection;
        _jetStream = jetStream;
        _serializerFactory = serializerFactory;
        _exceptionSerializer =
            serializerFactory.ExceptionSerializer() ??
            DumbExceptionSerializer.Instance;
        _eventObserver = new ObservableEvent<INotification>();
        _messageTracer = messageTracer ?? NullMessageTracer.Instance;
    }

    public ILoggerFactory LoggerFactory => _loggerFactory;
    public INatsConnection Connection => _connection;
    public INatsJSContext JetStream => _jetStream;

    public (INatsSerialize<T>?, IOutboundAdapter<T>?) Serializer<T>() =>
        _serializerFactory.PayloadSerializer<T>() switch {
            { } s => (s, null), _ => (null, _serializerFactory.OutboundAdapter<T>().ThrowIfNull())
        };
    
    public (INatsDeserialize<T>?, IInboundAdapter<T>?) Deserializer<T>() =>
        _serializerFactory.PayloadDeserializer<T>() switch {
            { } d => (d, null), _ => (null, _serializerFactory.InboundAdapter<T>().ThrowIfNull())
        };

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

    public Activity? SendActivity(string activityName, bool awaitsResponse) =>
        ActivitySource.StartActivity(
            activityName,
            awaitsResponse ? ActivityKind.Client : ActivityKind.Producer);

    public Activity? ReceiveActivity(
        string activityName, ActivityContext? context, bool awaitsResponse) =>
        ActivitySource.StartActivity(
            activityName,
            awaitsResponse ? ActivityKind.Server : ActivityKind.Consumer,
            context ?? default);

    public Activity? ReceiveActivity(
        string activityName, NatsHeaders? headers, bool awaitsResponse) =>
        ReceiveActivity(activityName, TryRestoreTrace(headers), awaitsResponse);
}
