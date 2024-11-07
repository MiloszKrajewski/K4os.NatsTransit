using System.Diagnostics;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Serialization;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

public class NatsInquirer
{
    public static NatsInquirer<TRequest, TResponse> Create<TRequest, TResponse>(
        NatsToolbox toolbox, TimeSpan timeout,
        OutboundAdapter<TRequest> serializer, InboundAdapter<TResponse> deserializer) =>
        new(toolbox, timeout, serializer, deserializer);
}

public class NatsInquirer<TRequest, TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly TimeSpan _timeout;
    private readonly Func<CancellationToken, Activity?, string, TRequest, Task<TResponse>> _inquirer;

    public NatsInquirer(
        NatsToolbox toolbox,
        TimeSpan timeout,
        OutboundAdapter<TRequest> serializer,
        InboundAdapter<TResponse> deserializer)
    {
        _toolbox = toolbox;
        _timeout = timeout;
        _inquirer = (serializer.Unpack(), deserializer.Unpack()) switch {
            ((var (s, ot), null), (var (d, it), null)) => (t, a, n, m) => Query(t, a, n, m, s, ot, d, it),
            ((null, var (s, ot)), (var (d, it), null)) => (t, a, n, m) => Query(t, a, n, m, s, ot, d, it),
            ((var (s, ot), null), (null, var (d, it))) => (t, a, n, m) => Query(t, a, n, m, s, ot, d, it),
            ((null, var (s, ot)), (null, var (d, it))) => (t, a, n, m) => Query(t, a, n, m, s, ot, d, it),
            _ => throw new InvalidOperationException("Misconfigured serializer")
        };
    }

    private async Task<TResponse> Query<TRequestPayload, TResponsePayload>(
        CancellationToken token,
        Activity? activity,
        string subject,
        TRequest request,
        INatsSerialize<TRequestPayload> serializer,
        IOutboundTransformer<TRequest, TRequestPayload> outboundTransformer,
        INatsDeserialize<TResponsePayload> deserializer,
        IInboundTransformer<TResponsePayload, TResponse> inboundTransformer)
    {
        try
        {
            activity?.OnSending(subject, request);
            var message = await _toolbox.Query(
                token, subject, request, serializer, outboundTransformer, deserializer, _timeout);
            activity?.OnReceived(message);
            var response = _toolbox.Unpack(message, inboundTransformer);
            activity?.OnUnpacked(response);
            return response;
        }
        catch (Exception error)
        {
            activity?.OnException(error);
            throw;
        }
    }

    public Task<TResponse> Query(CancellationToken token, Activity? activity, string subject, TRequest request) =>
        _inquirer(token, activity, subject, request);
}
