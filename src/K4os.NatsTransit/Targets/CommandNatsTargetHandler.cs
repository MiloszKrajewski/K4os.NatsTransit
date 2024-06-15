using K4os.NatsTransit.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

public class CommandNatsTargetHandler<TCommand>: 
    NatsTargetHandler<TCommand>
    where TCommand: IRequest
{
    protected readonly ILogger Log;

    private readonly string _subject;
    private readonly NatsToolbox _toolbox;
    private readonly INatsSerialize<TCommand> _serializer;

    public record Config(string Subject): INatsTargetConfig
    {
        public INatsTargetHandler CreateHandler(NatsToolbox toolbox) =>
            new CommandNatsTargetHandler<TCommand>(toolbox, this);
    }

    public CommandNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _subject = config.Subject;
        _serializer = toolbox.Serializer<TCommand>();
    }

    public override Task Handle(CancellationToken token, TCommand command) => 
        _toolbox.Publish(token, _subject, command, _serializer);
}
