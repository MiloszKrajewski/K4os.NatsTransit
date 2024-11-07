using System.Diagnostics;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Serialization;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

public class NatsRequester
{
    public static NatsRequester<TRequest, TResponse> Create<TRequest, TResponse>(
        NatsToolbox toolbox, 
        TimeSpan timeout,
        OutboundAdapter<TRequest> serializer, 
        InboundAdapter<TResponse> deserializer) =>
        new(toolbox, timeout, serializer, deserializer);
}

public class NatsRequester<TRequest, TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly TimeSpan _timeout;
    private readonly Func<CancellationToken, Activity?, string, TRequest, Task<TResponse>> _requester;

    public NatsRequester(
        NatsToolbox toolbox, TimeSpan timeout, 
        OutboundAdapter<TRequest> serializer, InboundAdapter<TResponse> deserializer)
    {
        _toolbox = toolbox;
        _timeout = timeout;
        _requester = (serializer.Unpack(), deserializer.Unpack()) switch {
            ((var (s, o), null), (var (d, i), null)) => (t, a, n, m) => Request(t, a, n, m, s, o, d, i),
            ((null, var (s, o)), (var (d, i), null)) => (t, a, n, m) => Request(t, a, n, m, s, o, d, i),
            ((var (s, o), null), (null, var (d, i))) => (t, a, n, m) => Request(t, a, n, m, s, o, d, i),
            ((null, var (s, o)), (null, var (d, i))) => (t, a, n, m) => Request(t, a, n, m, s, o, d, i),
            _ => throw new InvalidOperationException("Misconfigured serializer")
        };
    }
    
    private async Task<TResponse> Request<TRequestPayload, TResponsePayload>(
        CancellationToken token,
        Activity? activity,
        string subject, TRequest request,
        INatsSerialize<TRequestPayload> serializer,
        IOutboundTransformer<TRequest, TRequestPayload> outboundTransformer,
        INatsDeserialize<TResponsePayload> deserializer,
        IInboundTransformer<TResponsePayload, TResponse> inboundTransformer)
    {
        // some context why it is done this way:
        // https://github.com/nats-io/nats.py/discussions/221
        // long story short: JS does not have request/reply semantics, only CORE (non-durable)
        var replySubject = $"$reply.{Guid.NewGuid():N}-{DateTime.UtcNow.Ticks:x16}";
        var subscription = _toolbox.SubscribeOne(token, replySubject, _timeout, deserializer);
        var responseTask = /* no await */subscription.FirstOrDefault(token);
        activity?.OnSending(subject, request);
        await _toolbox.Request(token, subject, request, replySubject, serializer, outboundTransformer);
        var message = await responseTask;
        activity?.OnReceived(message);
        var response = _toolbox.Unpack(message, inboundTransformer);
        activity?.OnUnpacked(response);
        return response;
    }
    
    public Task<TResponse> Request(CancellationToken token, Activity? activity, string subject, TRequest request) =>
        _requester(token, activity, subject, request);
}
