using System.Diagnostics;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Serialization;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

public class NatsPublisher
{
    public static NatsPublisher<TMessage> Create<TMessage>(
        NatsToolbox toolbox, OutboundAdapter<TMessage> outboundAdapter) =>
        new(toolbox, outboundAdapter);
}

public class NatsPublisher<TMessage>
{
    private readonly Func<CancellationToken, Activity?, string, TMessage, ValueTask> _publisher;
    private readonly NatsToolbox _toolbox;

    public NatsPublisher(NatsToolbox toolbox, OutboundAdapter<TMessage> outboundAdapter)
    {
        _toolbox = toolbox;
        _publisher = outboundAdapter.Unpack() switch {
            (var (s, ot), null) => (t, a, n, m) => Publish(t, a, n, m, s, ot),
            (null, var (s, ot)) => (t, a, n, m) => Publish(t, a, n, m, s, ot),
            _ => throw new InvalidOperationException("Misconfigured serialization")
        };
    }

    private ValueTask Publish<TPayload>(
        CancellationToken token,
        Activity? activity,
        string subject, TMessage message,
        INatsSerialize<TPayload> serializer,
        IOutboundTransformer<TMessage, TPayload> transformer)
    {
        activity?.OnSending(subject, message);
        return _toolbox.Publish(token, subject, message, serializer, transformer);
    }
    
    public ValueTask Publish(CancellationToken token, Activity? activity, string subject, Exception error)
    {
        activity?.OnSending(subject, error);
        return _toolbox.Respond(token, subject, error);
    }

    public ValueTask Publish(CancellationToken token, Activity? activity, string subject, TMessage message) =>
        _publisher(token, activity, subject, message);
}
