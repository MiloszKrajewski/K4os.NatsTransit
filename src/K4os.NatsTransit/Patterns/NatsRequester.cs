using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

public class NatsRequester
{
    public static NatsRequester<TRequest, TResponse> Create<TRequest, TResponse>(
        NatsToolbox toolbox, 
        TimeSpan timeout,
        OutboundPair<TRequest> serializer, 
        InboundPair<TResponse> deserializer) =>
        new(toolbox, timeout, serializer, deserializer);
}

public class NatsRequester<TRequest, TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly TimeSpan _timeout;
    private readonly Func<CancellationToken, string, TRequest, Task<TResponse>> _requester;

    public NatsRequester(
        NatsToolbox toolbox, TimeSpan timeout, 
        OutboundPair<TRequest> serializer, InboundPair<TResponse> deserializer)
    {
        _toolbox = toolbox;
        _timeout = timeout;
        _requester = (serializer.Unpack(), deserializer.Unpack()) switch {
            ((var (s, o), null), (var (d, i), null)) => (t, n, m) => Request(t, n, m, s, o, d, i),
            ((null, var (s, o)), (var (d, i), null)) => (t, n, m) => Request(t, n, m, s, o, d, i),
            ((var (s, o), null), (null, var (d, i))) => (t, n, m) => Request(t, n, m, s, o, d, i),
            ((null, var (s, o)), (null, var (d, i))) => (t, n, m) => Request(t, n, m, s, o, d, i),
            _ => throw new InvalidOperationException("Misconfigured serializer")
        };
    }
    
    private async Task<TResponse> Request<TRequestPayload, TResponsePayload>(
        CancellationToken token,
        string subject, TRequest request,
        INatsSerialize<TRequestPayload> serializer,
        IOutboundAdapter<TRequest, TRequestPayload> outboundAdapter,
        INatsDeserialize<TResponsePayload> deserializer,
        IInboundAdapter<TResponsePayload, TResponse> inboundAdapter)
    {
        // some context why it is done this way:
        // https://github.com/nats-io/nats.py/discussions/221
        // long story short: JS does not have request/reply semantics, only CORE (non-durable)
        var replySubject = $"$reply.{Guid.NewGuid():N}-{DateTime.UtcNow.Ticks:x16}";
        var subscription = _toolbox.SubscribeOne(token, replySubject, _timeout, deserializer);
        var responseTask = /* no await */subscription.FirstOrDefault(token);
        await _toolbox.Request(token, subject, request, replySubject, serializer, outboundAdapter);
        var response = await responseTask;
        return _toolbox.Unpack(response, inboundAdapter);
    }
    
    public Task<TResponse> Request(CancellationToken token, string subject, TRequest request) =>
        _requester(token, subject, request);
}
