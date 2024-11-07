using System.Diagnostics;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Serialization;
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
        OutboundAdapter<TCommand>? OutboundAdapter = null
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
        var serializer = config.OutboundAdapter ?? toolbox.GetOutboundAdapter<TCommand>();
        _publisher = NatsPublisher.Create(toolbox, serializer);
    }

    private static string GetActivityName(Config config)
    {
        var subject = config.Subject;
        return $"Command({subject}).Send";
    }

    public override async Task Handle(CancellationToken token, TCommand command)
    {
        using var span = _toolbox.Tracing.SendingScope(_activityName, false);
        await _publisher.Publish(token, span, _subject, command);
        _toolbox.Metrics.MessageSent(_subject);
    }
}
