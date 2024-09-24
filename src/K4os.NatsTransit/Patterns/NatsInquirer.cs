using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

public class NatsInquirer
{
    public static NatsInquirer<TRequest, TResponse> Create<TRequest, TResponse>(
        NatsToolbox toolbox, TimeSpan timeout,
        OutboundPair<TRequest> serializer, InboundPair<TResponse> deserializer) =>
        new(toolbox, timeout, serializer, deserializer);
}

public class NatsInquirer<TRequest, TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly TimeSpan _timeout;
    private readonly Func<CancellationToken, string, TRequest, Task<TResponse>> _inquirer;

    public NatsInquirer(
        NatsToolbox toolbox,
        TimeSpan timeout,
        OutboundPair<TRequest> serializer,
        InboundPair<TResponse> deserializer)
    {
        _toolbox = toolbox;
        _timeout = timeout;
        _inquirer = (serializer.Unpack(), deserializer.Unpack()) switch {
            ((var (s, o), null), (var (d, i), null)) => (t, n, m) => Query(t, n, m, s, o, d, i),
            ((null, var (s, o)), (var (d, i), null)) => (t, n, m) => Query(t, n, m, s, o, d, i),
            ((var (s, o), null), (null, var (d, i))) => (t, n, m) => Query(t, n, m, s, o, d, i),
            ((null, var (s, o)), (null, var (d, i))) => (t, n, m) => Query(t, n, m, s, o, d, i),
            _ => throw new InvalidOperationException("Misconfigured serializer")
        };
    }

    private async Task<TResponse> Query<TRequestPayload, TResponsePayload>(
        CancellationToken token,
        string subject,
        TRequest request,
        INatsSerialize<TRequestPayload> serializer,
        IOutboundAdapter<TRequest, TRequestPayload> outboundAdapter,
        INatsDeserialize<TResponsePayload> deserializer,
        IInboundAdapter<TResponsePayload, TResponse> inboundAdapter)
    {
        var response = await _toolbox.Query(
            token, subject, request, serializer, outboundAdapter, deserializer, _timeout);
        return _toolbox.Unpack(response, inboundAdapter);
    }
    
    public Task<TResponse> Query(CancellationToken token, string subject, TRequest request) =>
        _inquirer(token, subject, request);
}
