using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using MediatR;

namespace K4os.NatsTransit.Sources;

public class EventNatsSourceHandler<TEvent>:
    NatsConsumeSourceHandler<TEvent>
    where TEvent: INotification
{
    public record Config(
        string Stream,
        string Consumer,
        IInboundAdapter<TEvent>? Adapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new EventNatsSourceHandler<TEvent>(toolbox, this);
    }

    public EventNatsSourceHandler(NatsToolbox toolbox, Config config):
        base(toolbox, config.Stream, config.Consumer, config.Adapter, config.Concurrency) { }
}