using System.Diagnostics.CodeAnalysis;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace K4os.NatsTransit.Extensions;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
internal static class NatsExtensions
{
    public static ValueTask Publish<TPayload>(
        this INatsConnection connection,
        CancellationToken token, 
        string subject, 
        INatsSerialize<TPayload>? serializer,
        NatsHeaders? headers, TPayload payload)
    {
        var message = new NatsMsg<TPayload> {
            Subject = subject,
            Headers = headers,
            Data = payload
        };
        return connection.PublishAsync(message, serializer, null, token);
    }
    
    public static ValueTask<PubAckResponse> Publish<TPayload>(
        this INatsJSContext jetStream,
        CancellationToken token, 
        string subject, 
        INatsSerialize<TPayload> serializer, 
        NatsHeaders? headers, TPayload payload)
    {
        return jetStream.PublishAsync(subject, payload, serializer, null, headers, token);
    }
    
    public static ValueTask<NatsMsg<TResponse>> Request<TRequest, TResponse>(
        this INatsConnection connection,
        CancellationToken token, 
        string subject, 
        INatsSerialize<TRequest> serializer, INatsDeserialize<TResponse> deserializer, 
        TimeSpan timeout,
        NatsHeaders? headers, TRequest payload)
    {
        var message = new NatsMsg<TRequest> {
            Subject = subject,
            Headers = headers,
            Data = payload,
        };
        var natsSubOpts = new NatsSubOpts { MaxMsgs = 1, StartUpTimeout = timeout };
        return connection.RequestAsync(
            message,
            serializer, deserializer,
            null, natsSubOpts,
            token
        );
    }

    public static ValueTask Respond<TRequest, TResponse>(
        this NatsMsg<TRequest> request, 
        CancellationToken token, 
        INatsSerialize<TResponse>? serializer, 
        NatsHeaders? headers, TResponse payload)
    {
        var message = new NatsMsg<TResponse> {
            Headers = headers,
            Data = payload
        };
        return request.ReplyAsync(message, serializer, null, token);
    }
}
