using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

// using var _ = _toolbox.SendActivity(_activityName, false);

public class NatsPublisher
{
    public static NatsPublisher<TMessage> Create<TMessage>(
        NatsToolbox toolbox, OutboundAdapter<TMessage> serializer) =>
        new(toolbox, serializer);
}

public class NatsPublisher<TMessage>
{
    private readonly Func<CancellationToken, string, TMessage, ValueTask> _publisher;
    private readonly NatsToolbox _toolbox;

    public NatsPublisher(NatsToolbox toolbox, OutboundAdapter<TMessage> serializer)
    {
        _toolbox = toolbox;
        _publisher = serializer.Unpack() switch {
            (var (s, a), null) => (t, n, m) => Publish(t, n, m, s, a),
            (null, var (s, a)) => (t, n, m) => Publish(t, n, m, s, a),
            _ => throw new InvalidOperationException("Misconfigured serialization")
        };
    }
   
    private ValueTask Publish<TPayload>(
        CancellationToken token,
        string subject, TMessage message,
        INatsSerialize<TPayload> serializer,
        IOutboundTransformer<TMessage, TPayload> transformer) =>
        _toolbox.Publish(token, subject, message, serializer, transformer);

    public ValueTask Publish(CancellationToken token, string subject, TMessage message) =>
        _publisher(token, subject, message);
}
