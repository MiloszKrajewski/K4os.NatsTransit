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
        base(
            toolbox, 
            config.Stream, config.Consumer, GetActivityName(config),
            false,
            config.Adapter, 
            config.Concurrency) { }
    
    private static string GetActivityName(Config config)
    {
        var eventType = typeof(TEvent).Name;
        var streamName = config.Stream;
        var consumerName = config.Consumer;
        return $"Consume<{eventType}>({streamName}/{consumerName})";
    }


    protected override void OnMessage(NatsToolbox toolbox, TEvent content) => 
        toolbox.OnEvent(content);
}