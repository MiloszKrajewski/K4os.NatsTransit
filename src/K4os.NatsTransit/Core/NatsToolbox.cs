using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public class NatsToolbox
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly INatsConnection _connection;
    private readonly INatsJSContext _jetStream;
    private readonly INatsSerializerFactory _serializerFactory;
    private readonly IExceptionSerializer _exceptionSerializer;

    public NatsToolbox(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext jetStream,
        INatsSerializerFactory serializerFactory,
        IExceptionSerializer exceptionSerializer)
    {
        _loggerFactory = loggerFactory;
        _connection = connection;
        _jetStream = jetStream;
        _serializerFactory = serializerFactory;
        _exceptionSerializer = exceptionSerializer;
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

    public Task Publish<T>(
        CancellationToken token,
        string subject, T payload,
        INatsSerialize<T> serializer)
    {
        var message = new NatsMsg<T> { Subject = subject, Data = payload };
        return _connection.PublishAsync(message, serializer, null, token).AsTask();
    }
    
    public Task Respond<TRequest, TResponse>(
        CancellationToken token,
        NatsMsg<TRequest> request, TResponse response,
        INatsSerialize<TResponse> serializer)
    {
        var replyTo = request.ReplyTo;
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        return Respond(token, replyTo, response, serializer);
    }
    
    public Task Respond<TRequest>(
        CancellationToken token,
        NatsMsg<TRequest> request, Exception exception)
    {
        var replyTo = request.ReplyTo;
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        return Respond(token, replyTo, exception);
    }
    
    private static string? GetReplyToSubject<T>(NatsJSMsg<T> message) =>
        message.Headers?.TryGetValue(NatsConstants.ReplyToHeaderName, out var value) ?? false
            ? value.ToString()
            : null;
    
    public Task Respond<TRequest, TResponse>(
        CancellationToken token,
        NatsJSMsg<TRequest> request, TResponse response,
        INatsSerialize<TResponse> serializer)
    {
        var replyTo = GetReplyToSubject(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        return Respond(token, replyTo, response, serializer);
    }
    
    public Task Respond<TRequest>(
        CancellationToken token,
        NatsJSMsg<TRequest> request, Exception exception)
    {
        var replyTo = GetReplyToSubject(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        return Respond(token, replyTo, exception);
    }

    public Task Respond<T>(
        CancellationToken token,
        string subject, T payload,
        INatsSerialize<T> serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var message = new NatsMsg<T> { Subject = subject, Data = payload };
        return _connection.PublishAsync(message, serializer, null, token).AsTask();
    }

    public Task Respond(
        CancellationToken token,
        string subject, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var payload = _exceptionSerializer.Serialize(exception);
        var headers = new NatsHeaders { { NatsConstants.ErrorHeaderName, payload } };
        var message = new NatsMsg<object?> { Subject = subject, Headers = headers };
        return _connection.PublishAsync(message, null, null, token).AsTask();
    }

    public Task Request<T>(
        CancellationToken token,
        string subject, T payload, string replySubject,
        INatsSerialize<T> serializer)
    {
        var headers = new NatsHeaders { { NatsConstants.ReplyToHeaderName, replySubject } };
        var message = new NatsMsg<T> { Subject = subject, Data = payload, Headers = headers };
        return _connection.PublishAsync(message, serializer, null, token).AsTask();
    }

    public Task<NatsMsg<TResponse>> Query<TRequest, TResponse>(
        CancellationToken token,
        string subject, TRequest request,
        INatsSerialize<TRequest> serializer,
        INatsDeserialize<TResponse> deserializer,
        TimeSpan timeout)
    {
        var message = new NatsMsg<TRequest> { Subject = subject, Data = request };
        var natsSubOpts = new NatsSubOpts { MaxMsgs = 1, StartUpTimeout = timeout };
        return _connection.RequestAsync(
            message,
            serializer, deserializer,
            null, natsSubOpts,
            token
        ).AsTask();
    }

    public T Accept<T>(NatsMsg<T> message)
    {
        message.EnsureSuccess();
        var payload = message.Headers?[NatsConstants.ErrorHeaderName].ToString();
        var exception = payload is null ? null : _exceptionSerializer.Deserialize(payload);
        if (exception is not null) throw exception;

        return message.Data!;
    }

    public async Task WaitAndKeepAlive<TRequest>(
        NatsJSMsg<TRequest> message, Task action, CancellationToken token)
    {
        await action.KeepAlive(
            t => message.AckProgressAsync(null, t).AsTask(),
            NatsConstants.KeepAliveInterval,
            token);
        await message.AckAsync(null, token);
    }
}
