namespace K4os.NatsTransit.Abstractions.Serialization;

public interface INatsSerializerFactory
{
    OutboundAdapter<T> GetOutboundAdapter<T>();
    InboundAdapter<T> GetInboundAdapter<T>();
}
