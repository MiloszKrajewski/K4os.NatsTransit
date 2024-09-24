namespace K4os.NatsTransit.Abstractions.MessageBus;

public interface IFluentNats
{
    IFluentNatsTopic WithTopic(string name);
}
