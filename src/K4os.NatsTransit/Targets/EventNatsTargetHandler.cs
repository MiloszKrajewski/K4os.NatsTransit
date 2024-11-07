using K4os.NatsTransit.Core;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Serialization;
using MediatR;

namespace K4os.NatsTransit.Targets;

public class EventNatsTargetHandler<TEvent>:
    NatsTargetHandler<TEvent>
    where TEvent: INotification
{
    private readonly string _subject;
    private readonly string _activityName;
    private readonly NatsPublisher<TEvent> _publisher;
    private readonly NatsToolbox _toolbox;

    public record Config(
        string Subject,
        OutboundAdapter<TEvent>? OutboundPair = null
    ): INatsTargetConfig
    {
        public INatsTargetHandler CreateHandler(NatsToolbox toolbox) =>
            new EventNatsTargetHandler<TEvent>(toolbox, this);
    }

    public EventNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        _toolbox = toolbox;
        _subject = config.Subject;
        _activityName = GetActivityName(config);
        var serializer = config.OutboundPair ?? toolbox.GetOutboundAdapter<TEvent>();
        _publisher = NatsPublisher.Create(toolbox, serializer);
    }

    private static string GetActivityName(Config config)
    {
        var subject = config.Subject;
        return $"Event({subject}).Send";
    }

    public override async Task Handle(CancellationToken token, TEvent @event)
    {
        using var span = _toolbox.Tracing.SendingScope(_activityName, false);
        await _publisher.Publish(token, span, _subject, @event);
        _toolbox.Metrics.MessageSent(_subject);
    }
}
