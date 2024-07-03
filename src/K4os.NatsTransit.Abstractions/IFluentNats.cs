namespace K4os.NatsTransit.Abstractions;

public interface IFluentNats
{
    IFluentNatsTopic WithTopic(string name);
}
