using K4os.NatsTransit.Abstractions.MessageBus;

namespace K4os.NatsTransit.Configuration;

public interface IFluentNats
{
    IFluentNatsTopic WithTopic(string name, StreamType streamType = StreamType.Default);
}
