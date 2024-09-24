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
    private static readonly NatsRawSerializer<IMemoryOwner<byte>> BinaryDeserializer =
        NatsRawSerializer<IMemoryOwner<byte>>.Default;

    internal static string? TryGetHeaderString(this NatsHeaders? headers, string key) =>
        headers?.TryGetValue(key, out var value) ?? false ? value.ToString() : null;
    
    internal static string? TryGetReplyTo<T>(this NatsJSMsg<T> message) =>
        message.ReplyTo ?? message.Headers.TryGetHeaderString(NatsConstants.ReplyToHeaderName);

    internal static string? TryGetReplyTo(this NatsHeaders? headers) =>
        headers.TryGetHeaderString(NatsConstants.ReplyToHeaderName);

    internal static string? TryGetError(this NatsHeaders? headers) =>
        headers.TryGetHeaderString(NatsConstants.ErrorHeaderName);

    internal static string? TryGetKnownType(this NatsHeaders? headers) =>
        headers.TryGetHeaderString(NatsConstants.KnownTypeHeaderName);

    public static
        ((INatsSerialize<T>, NullOutboundAdapter<T>)?, (NatsRawSerializer<IBufferWriter<byte>>, IOutboundAdapter<T>)?)
        Unpack<T>(this OutboundPair<T> serializer) =>
        serializer.Native is { } native
            ? ((native, NullOutboundAdapter<T>.Default), null)
            : (null, (NatsRawSerializer<IBufferWriter<byte>>.Default, serializer.Adapter.ThrowIfNull()));

    public static
        ((INatsDeserialize<T>, NullInboundAdapter<T>)?, (NatsRawSerializer<IMemoryOwner<byte>>, IInboundAdapter<T>)?)
        Unpack<T>(this InboundPair<T> deserializer) =>
        deserializer.Native is { } native
            ? ((native, NullInboundAdapter<T>.Default), null)
            : (null, (NatsRawSerializer<IMemoryOwner<byte>>.Default, deserializer.Adapter.ThrowIfNull()));

    public static ValueTask Publish<TMessage>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TMessage payload,
        OutboundPair<TMessage> serializer) =>
        serializer.Unpack() switch {
            (var (s, a), null) => toolbox.Publish(token, subject, payload, s, a),
            (null, var (s, a)) => toolbox.Publish(token, subject, payload, s, a),
            _ => default // this will not happen as 'Unpack' guarantees one of the branches
        };

    public static ValueTask Respond<TRequest, TResponse>(
        this NatsToolbox toolbox,
        CancellationToken token,
        NatsMsg<TRequest> request, TResponse payload,
        OutboundPair<TResponse> serializer) =>
        serializer.Unpack() switch {
            (var (s, a), null) => toolbox.Respond(token, request, payload, s, a),
            (null, var (s, a)) => toolbox.Respond(token, request, payload, s, a),
            _ => default // this will not happen as 'Unpack' guarantees one of the branches
        };

    // public static ValueTask Respond<TRequest, TResponse>(
    //     this NatsToolbox toolbox,
    //     CancellationToken token,
    //     NatsJSMsg<TRequest> request, TResponse payload,
    //     OutboundPair<TResponse> serializer) =>
    //     toolbox.Publish(token, GetReplyTo(request), payload, serializer);
    //
    // public static ValueTask Respond<TRequest>(
    //     this NatsToolbox toolbox,
    //     CancellationToken token,
    //     NatsJSMsg<TRequest> request, Exception error) =>
    //     toolbox.Respond(token, GetReplyTo(request), error);

    public static ValueTask<NatsMsg<TResponsePayload>> Query<TRequest, TResponsePayload>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TRequest request,
        OutboundPair<TRequest> serializer,
        INatsDeserialize<TResponsePayload> deserializer,
        TimeSpan timeout) =>
        serializer.Unpack() switch {
            (var (s, a), null) => toolbox.Query(token, subject, request, s, a, deserializer, timeout),
            (null, var (s, a)) => toolbox.Query(token, subject, request, s, a, deserializer, timeout),
            _ => default // this will not happen as 'Unpack' guarantees one of the branches
        };

    public static ValueTask<NatsMsg<IMemoryOwner<byte>>> Query<TRequest>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TRequest request,
        OutboundPair<TRequest> serializer,
        TimeSpan timeout) =>
        serializer.Unpack() switch {
            (var (s, a), null) => toolbox.Query(token, subject, request, s, a, BinaryDeserializer, timeout),
            (null, var (s, a)) => toolbox.Query(token, subject, request, s, a, BinaryDeserializer, timeout),
            _ => default // this will not happen as 'Unpack' guarantees one of the branches
        };

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

    [Obsolete("Use NoAck version")]
    public static ValueTask WaitAndKeepAlive<TRequest>(
        this NatsJSMsg<TRequest> message, CancellationToken token, Task action) =>
        action.IsCompletedSuccessfully 
            ? message.AckAsync(null, token) // fast path, completed immediately, no error 
            : new ValueTask(WaitAndKeepAliveLoop(message, action, token));

    private static async Task WaitAndKeepAliveLoop<TRequest>(
        NatsJSMsg<TRequest> message, Task action, CancellationToken token)
    {
        await action.KeepAlive(
            t => message.AckProgressAsync(null, t),
            NatsConstants.KeepAliveInterval,
            token);
        await action; // throw exception if needed?

        await message.AckAsync(null, token);
    }
    
    public static Task WaitAndKeepAliveNoAck<TRequest>(
        this NatsJSMsg<TRequest> message, Task action, CancellationToken token) =>
        action.KeepAlive(
            t => message.AckProgressAsync(null, t),
            NatsConstants.KeepAliveInterval,
            token);
}
