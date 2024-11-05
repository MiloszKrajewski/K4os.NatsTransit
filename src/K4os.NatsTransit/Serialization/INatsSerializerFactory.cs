namespace K4os.NatsTransit.Serialization;

public interface INatsSerializerFactory
{
    OutboundAdapter<T> GetOutboundAdapter<T>();
    InboundAdapter<T> GetInboundAdapter<T>();
}
