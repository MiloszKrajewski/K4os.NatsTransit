using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public static class NatsToolboxExtensions
{
    private static readonly NatsRawSerializer<IBufferWriter<byte>> BinarySerializer =
        NatsRawSerializer<IBufferWriter<byte>>.Default;

    private static readonly NatsRawSerializer<IMemoryOwner<byte>> BinaryDeserializer =
        NatsRawSerializer<IMemoryOwner<byte>>.Default;

    private static string GetReplyTo<TRequest>(NatsMsg<TRequest> request) =>
        request.Headers.TryGetReplyTo() ?? request.ReplyTo ??
        throw new InvalidOperationException("Message does not have reply-to header");

    private static string GetReplyTo<TRequest>(NatsJSMsg<TRequest> request) =>
        request.Headers.TryGetReplyTo() ?? request.ReplyTo ??
        throw new InvalidOperationException("Message does not have reply-to header");

    internal static string? TryGetReplyTo(this NatsHeaders? headers) =>
        headers.TryGetHeaderString(NatsConstants.ReplyToHeaderName);

    internal static string? TryGetError(this NatsHeaders? headers) =>
        headers.TryGetHeaderString(NatsConstants.ErrorHeaderName);

    internal static string? TryGetKnownType(this NatsHeaders? headers) =>
        headers.TryGetHeaderString(NatsConstants.KnownTypeHeaderName);

    internal static string? TryGetHeaderString(this NatsHeaders? headers, string key) =>
        headers?.TryGetValue(key, out var value) ?? false ? value.ToString() : null;

    public static ValueTask Publish<TMessage>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TMessage payload,
        INatsSerialize<TMessage> serializer) =>
        toolbox.Publish(
            token, 
            subject, payload, 
            serializer, NullOutboundAdapter<TMessage>.Default);

    public static ValueTask Publish<TMessage>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TMessage payload,
        IOutboundAdapter<TMessage> adapter) =>
        toolbox.Publish(token, subject, payload, BinarySerializer, adapter);

    public static ValueTask Respond<TRequest, TResponse>(
        this NatsToolbox toolbox,
        CancellationToken token,
        NatsMsg<TRequest> request, TResponse response,
        INatsSerialize<TResponse> serializer) =>
        toolbox.Respond(
            token, 
            request, response, 
            serializer, NullOutboundAdapter<TResponse>.Default);

    public static ValueTask Respond<TRequest, TResponse>(
        this NatsToolbox toolbox,
        CancellationToken token,
        NatsMsg<TRequest> request, TResponse response,
        IOutboundAdapter<TResponse> adapter) =>
        toolbox.Respond(token, request, response, BinarySerializer, adapter);

    public static ValueTask Respond<TRequest, TResponse>(
        this NatsToolbox toolbox,
        CancellationToken token,
        NatsJSMsg<TRequest> request, TResponse response,
        INatsSerialize<TResponse> serializer) =>
        toolbox.Publish(token, GetReplyTo(request), response, serializer);

    public static ValueTask Respond<TRequest, TResponse>(
        this NatsToolbox toolbox,
        CancellationToken token,
        NatsJSMsg<TRequest> request, TResponse response,
        IOutboundAdapter<TResponse> adapter) =>
        toolbox.Publish(token, GetReplyTo(request), response, adapter);

    public static ValueTask Respond<TRequest>(
        this NatsToolbox toolbox,
        CancellationToken token,
        NatsJSMsg<TRequest> request, Exception response) =>
        toolbox.Publish(token, GetReplyTo(request), response);

    public static ValueTask<NatsMsg<TResponsePayload>> Query<TRequest, TResponsePayload>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TRequest request,
        INatsSerialize<TRequest> serializer,
        INatsDeserialize<TResponsePayload> deserializer,
        TimeSpan timeout) =>
        toolbox.Query(
            token, subject, request,
            serializer, NullOutboundAdapter<TRequest>.Default,
            deserializer,
            timeout);

    public static ValueTask<NatsMsg<TResponsePayload>> Query<TRequest, TResponsePayload>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TRequest request,
        IOutboundAdapter<TRequest> adapter,
        INatsDeserialize<TResponsePayload> deserializer,
        TimeSpan timeout) =>
        toolbox.Query(token, subject, request, BinarySerializer, adapter, deserializer, timeout);

    public static ValueTask<NatsMsg<IMemoryOwner<byte>>> Query<TRequest>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TRequest request,
        INatsSerialize<TRequest> serializer,
        TimeSpan timeout) =>
        toolbox.Query(token, subject, request, serializer, BinaryDeserializer, timeout);

    public static ValueTask<NatsMsg<IMemoryOwner<byte>>> Query<TRequest>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TRequest request,
        IOutboundAdapter<TRequest> adapter,
        TimeSpan timeout) =>
        toolbox.Query(token, subject, request, adapter, BinaryDeserializer, timeout);

    public static TMessage Unpack<TPayload, TMessage>(
        this NatsToolbox toolbox,
        NatsMsg<TPayload> message,
        IInboundAdapter<TPayload, TMessage> adapter)
    {
        message.EnsureSuccess();
        return toolbox.Unpack(null, message.Subject, message.Headers, message.Data, adapter);
    }

    public static TMessage Unpack<TPayload, TMessage>(
        this NatsToolbox toolbox, NatsJSMsg<TPayload> message,
        IInboundAdapter<TPayload, TMessage> adapter)
    {
        message.EnsureSuccess();
        return toolbox.Unpack(null, message.Subject, message.Headers, message.Data, adapter);
    }

    public static TMessage Unpack<TMessage>(
        this NatsToolbox toolbox, NatsMsg<TMessage> message) =>
        toolbox.Unpack(message, NullInboundAdapter<TMessage>.Default);

    public static TMessage Unpack<TMessage>(
        this NatsToolbox toolbox, NatsJSMsg<TMessage> message) =>
        toolbox.Unpack(message, NullInboundAdapter<TMessage>.Default);

    public static async Task WaitAndKeepAlive<TRequest>(
        this NatsJSMsg<TRequest> message, CancellationToken token, Task action)
    {
        await action.KeepAlive(
            t => message.AckProgressAsync(null, t),
            NatsConstants.KeepAliveInterval,
            token);
        await action; // throw exception if needed?
        await message.AckAsync(null, token);
    }
}
