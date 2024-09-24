using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
using MediatR;

namespace K4os.NatsTransit.Targets;

public class CommandNatsTargetHandler<TCommand>:
    NatsTargetHandler<TCommand>
    where TCommand: IRequest
{
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly string _activityName;
    private readonly NatsPublisher<TCommand> _publisher;

    public record Config(
        string Subject,
        OutboundAdapter<TCommand>? OutboundPair = null
    ): INatsTargetConfig
    {
        public INatsTargetHandler CreateHandler(NatsToolbox toolbox) =>
            new CommandNatsTargetHandler<TCommand>(toolbox, this);
    }

    public CommandNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        _toolbox = toolbox;
        _subject = config.Subject;
        _activityName = GetActivityName(config);
        var serializer = config.OutboundPair ?? toolbox.Serializer<TCommand>();
        _publisher = NatsPublisher.Create(toolbox, serializer);
    }

    private static string GetActivityName(Config config)
    {
        var commandType = typeof(TCommand).GetFriendlyName();
        var subject = config.Subject;
        return $"Command<{commandType}>({subject})";
    }

    public override async Task Handle(CancellationToken token, TCommand command)
    {
        using var _ = _toolbox.SendActivity(_activityName, false);
        await _publisher.Publish(token, _subject, command).AsTask();
    }
}
