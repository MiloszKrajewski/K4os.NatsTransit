using K4os.NatsTransit.Abstractions;
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
    private readonly IOutboundAdapter<TCommand>? _adapter;
    private string _activityName;

    public record Config(
        string Subject,
        IOutboundAdapter<TCommand>? Adapter = null
    ): INatsTargetConfig
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
        _adapter = config.Adapter;
        var commandType = typeof(TCommand).Name;
        _activityName = $"Command<{commandType}>({_subject})";
    }

    public override async Task Handle(CancellationToken token, TCommand command)
    {
        using var _ = _toolbox.SendActivity(_activityName);
        var sent = _adapter is null
            ? Handle(token, command, _serializer, NullOutboundAdapter)
            : Handle(token, command, BinarySerializer, _adapter);
        await sent;
    }

    public Task Handle<TPayload>(
        CancellationToken token, TCommand command,
        INatsSerialize<TPayload> serializer,
        IOutboundAdapter<TCommand, TPayload> adapter) =>
        _toolbox.Publish(token, _subject, command, serializer, adapter).AsTask();
}
