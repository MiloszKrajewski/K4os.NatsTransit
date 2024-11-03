using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Extensions;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public static class NatsToolboxExtensions
{
    internal static NatsHeaders? ToNatsHeaders(this Dictionary<string, StringValues>? headers) =>
        headers is null ? null : new NatsHeaders(headers);

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

    internal static (
        (INatsSerialize<T>, NullOutboundTransformer<T>)?,
        (NatsRawSerializer<Memory<byte>>, ICustomSerializer<T>)?)
        Unpack<T>(this OutboundAdapter<T> serializer) =>
        serializer.Native is { } native
            ? ((native, NullOutboundTransformer<T>.Default), null)
            : (null, (NatsRawSerializer<Memory<byte>>.Default, serializer.Custom.ThrowIfNull()));

    internal static
        ((INatsDeserialize<T>, NullInboundTransformer<T>)?,
        (NatsRawSerializer<IMemoryOwner<byte>>, ICustomDeserializer<T>)?)
        Unpack<T>(this InboundAdapter<T> deserializer) =>
        deserializer.Native is { } native
            ? ((native, NullInboundTransformer<T>.Default), null)
            : (null, (NatsRawSerializer<IMemoryOwner<byte>>.Default, deserializer.Custom.ThrowIfNull()));

    public static ValueTask Publish<TMessage>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TMessage payload,
        OutboundAdapter<TMessage> serializer) =>
        serializer.Unpack() switch {
            (var (s, a), null) => toolbox.Publish(token, subject, payload, s, a),
            (null, var (s, a)) => toolbox.Publish(token, subject, payload, s, a),
            _ => default // this will not happen as 'Unpack' guarantees one of the branches
        };

    public static ValueTask Respond<TRequest, TResponse>(
        this NatsToolbox toolbox,
        CancellationToken token,
        NatsMsg<TRequest> request, TResponse payload,
        OutboundAdapter<TResponse> serializer) =>
        serializer.Unpack() switch {
            (var (s, a), null) => toolbox.Respond(token, request, payload, s, a),
            (null, var (s, a)) => toolbox.Respond(token, request, payload, s, a),
            _ => default // this will not happen as 'Unpack' guarantees one of the branches
        };

    public static ValueTask<NatsMsg<TResponsePayload>> Query<TRequest, TResponsePayload>(
        this NatsToolbox toolbox,
        CancellationToken token,
        string subject, TRequest request,
        OutboundAdapter<TRequest> serializer,
        INatsDeserialize<TResponsePayload> deserializer,
        TimeSpan timeout) =>
        serializer.Unpack() switch {
            (var (s, a), null) => toolbox.Query(token, subject, request, s, a, deserializer, timeout),
            (null, var (s, a)) => toolbox.Query(token, subject, request, s, a, deserializer, timeout),
            _ => default // this will not happen as 'Unpack' guarantees one of the branches
        };

    public static TMessage Unpack<TPayload, TMessage>(
        this NatsToolbox toolbox,
        NatsMsg<TPayload> message,
        IInboundTransformer<TPayload, TMessage> transformer)
    {
        message.EnsureSuccess();
        return toolbox.Unpack(null, message.Subject, message.Headers, message.Data, transformer);
    }

    public static TMessage Unpack<TPayload, TMessage>(
        this NatsToolbox toolbox, NatsJSMsg<TPayload> message,
        IInboundTransformer<TPayload, TMessage> transformer)
    {
        message.EnsureSuccess();
        return toolbox.Unpack(null, message.Subject, message.Headers, message.Data, transformer);
    }

    public static TMessage Unpack<TMessage>(
        this NatsToolbox toolbox, NatsMsg<TMessage> message) =>
        toolbox.Unpack(message, NullInboundTransformer<TMessage>.Default);

    public static TMessage Unpack<TMessage>(
        this NatsToolbox toolbox, NatsJSMsg<TMessage> message) =>
        toolbox.Unpack(message, NullInboundTransformer<TMessage>.Default);

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
