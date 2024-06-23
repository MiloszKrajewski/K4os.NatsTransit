using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

public class EventNatsTargetHandler<TEvent>:
    NatsTargetHandler<TEvent>
    where TEvent: INotification
{
    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly INatsSerialize<TEvent> _serializer;
    private readonly IOutboundAdapter<TEvent>? _adapter;

    public record Config(
        string Subject,
        IOutboundAdapter<TEvent>? Adapter = null
    ): INatsTargetConfig
    {
        public INatsTargetHandler CreateHandler(NatsToolbox toolbox) =>
            new EventNatsTargetHandler<TEvent>(toolbox, this);
    }

    public EventNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _subject = config.Subject;
        _serializer = toolbox.Serializer<TEvent>();
        _adapter = config.Adapter;
    }

    public override Task Handle(CancellationToken token, TEvent @event) =>
        _adapter is null
            ? Handle(token, @event, _serializer, NullOutboundAdapter)
            : Handle(token, @event, BinarySerializer, _adapter);

    public Task Handle<TPayload>(
        CancellationToken token, TEvent @event,
        INatsSerialize<TPayload> serializer,
        IOutboundAdapter<TEvent, TPayload> adapter) =>
        _toolbox.Publish(token, _subject, @event, serializer, adapter).AsTask();
}
