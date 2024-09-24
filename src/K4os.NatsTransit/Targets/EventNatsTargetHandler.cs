using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
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
        var serializer = config.OutboundPair ?? toolbox.Serializer<TEvent>();
        _publisher = NatsPublisher.Create(toolbox, serializer);
    }

    private static string GetActivityName(Config config)
    {
        var eventType = typeof(TEvent).GetFriendlyName();
        var subject = config.Subject;
        return $"Event<{eventType}>({subject})";
    }

    public override async Task Handle(CancellationToken token, TEvent @event)
    {
        using var _ = _toolbox.SendActivity(_activityName, false);
        await _publisher.Publish(token, _subject, @event);
    }
}
