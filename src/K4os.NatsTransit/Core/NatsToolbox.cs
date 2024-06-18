using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core.Messages;
using K4os.NatsTransit.Extensions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using ReceivedBuffer = NATS.Client.Core.NatsMemoryOwner<byte>;
using OutgoingBuffer = NATS.Client.Core.NatsBufferWriter<byte>;

namespace K4os.NatsTransit.Core;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public class NatsToolbox
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly INatsConnection _connection;
    private readonly INatsJSContext _jetStream;
    private readonly IPayloadSerializer _payloadSerializer;
    private readonly IExceptionSerializer _exceptionSerializer;
    private readonly IKnownTypeResolver _knownTypeResolver;

    public NatsToolbox(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext jetStream,
        IPayloadSerializer payloadSerializer,
        IExceptionSerializer exceptionSerializer, 
        IKnownTypeResolver knownTypeResolver)
    {
        _loggerFactory = loggerFactory;
        _connection = connection;
        _jetStream = jetStream;
        _payloadSerializer = payloadSerializer;
        _exceptionSerializer = exceptionSerializer;
        _knownTypeResolver = knownTypeResolver;
    }

    public ILoggerFactory LoggerFactory => _loggerFactory;
    public INatsConnection Connection => _connection;
    public INatsJSContext JetStream => _jetStream;

    private NatsRawSerializer<ReceivedBuffer> BytesDeserializer =>
        NatsRawSerializer<ReceivedBuffer>.Default;
    
    private NatsRawSerializer<OutgoingBuffer> BytesSerializer =>
        NatsRawSerializer<OutgoingBuffer>.Default;

    public ILogger GetLogger(object component) =>
        _loggerFactory.CreateLogger(component.GetType().GetFriendlyName());

    public IAsyncEnumerable<IReceivedMessage> SubscribeOne(
        CancellationToken token, string subject, TimeSpan timeout)
    {
        var options = new NatsSubOpts { MaxMsgs = 1, StartUpTimeout = timeout };
        var messages = _connection.SubscribeAsync(
            subject, null, BytesDeserializer, options, token);
        return DeserializeMany(messages, token);
    }

    public IAsyncEnumerable<IReceivedMessage> SubscribeMany(
        CancellationToken token, string subject)
    {
        var options = default(NatsSubOpts);
        var messages = _connection.SubscribeAsync(
            subject, null, BytesDeserializer, options, token);
        return DeserializeMany(messages, token);
    }

    public async Task<IAsyncEnumerable<IReceivedMessage>> ConsumeMany(
        CancellationToken token, string stream, string consumer)
    {
        var subscription = await _jetStream.GetConsumerAsync(stream, consumer, token);
        var messages = subscription.ConsumeAsync(BytesDeserializer, null, token);
        return DeserializeMany(messages, token);
    }
    
    private async IAsyncEnumerable<IReceivedMessage> DeserializeMany(
        IAsyncEnumerable<NatsMsg<ReceivedBuffer>> messages,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var message in messages.WithCancellation(token))
            yield return DeserializeOne(message);
    }

    private async IAsyncEnumerable<IReceivedMessage> DeserializeMany(
        IAsyncEnumerable<NatsJSMsg<ReceivedBuffer>> messages,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var message in messages.WithCancellation(token))
            yield return DeserializeOne(message);
    }
    
    private IReceivedMessage DeserializeOne(NatsMsg<ReceivedBuffer> message) =>
        ReceivedMessage.Create(
            message, 
            _knownTypeResolver, 
            _payloadSerializer, 
            _exceptionSerializer);
    
    private IReceivedMessage DeserializeOne(NatsJSMsg<ReceivedBuffer> message) =>
        ReceivedMessage.Create(
            message, 
            _knownTypeResolver, 
            _payloadSerializer, 
            _exceptionSerializer);

    public Task Publish(CancellationToken token, string subject, object? payload)
    {
        var writer = new OutgoingBuffer();
        _payloadSerializer.Serialize(payload, writer);
        var knownType = _knownTypeResolver.Resolve(payload?.GetType());
        var headers = default(NatsHeaders)
            .Extend(NatsConstants.KnownTypeHeaderName, knownType)
            .Extend(
        var message = new NatsMsg<OutgoingBuffer> {
            Subject = subject,
            Data = writer,
        };
        return _connection.PublishAsync(message, ByteBufferSerializer, null, token).AsTask();
    }

    public Task Respond<T>(
        CancellationToken token, NatsMsg<T> request, ByteBuffer response)
    {
        var replyTo = request.ReplyTo;
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        return Respond(token, replyTo, response, ByteBufferSerializer);
    }

    public Task Respond<TRequest>(
        CancellationToken token,
        NatsMsg<TRequest> request, Exception exception)
    {
        var replyTo = request.ReplyTo;
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        return Respond(token, replyTo, exception);
    }

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
