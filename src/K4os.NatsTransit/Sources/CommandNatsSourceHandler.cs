using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using MediatR;

namespace K4os.NatsTransit.Sources;

public class CommandNatsSourceHandler<TCommand>:
    NatsConsumeSourceHandler<TCommand>
    where TCommand: IRequest
{
    public record Config(
        string Stream,
        string Consumer,
        IInboundAdapter<TCommand>? Adapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new CommandNatsSourceHandler<TCommand>(toolbox, this);
    }

    public CommandNatsSourceHandler(NatsToolbox toolbox, Config config):
        base(
            toolbox,
            config.Stream, config.Consumer, GetActivityName(config),
            false,
            config.Adapter,
            config.Concurrency) { }

    private static string GetActivityName(Config config)
    {
        var commandType = typeof(TCommand).Name;
        var streamName = config.Stream;
        var consumerName = config.Consumer;
        return $"Consume<{commandType}>({streamName}/{consumerName}))";
    }
}
