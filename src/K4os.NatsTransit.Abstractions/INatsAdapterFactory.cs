namespace K4os.NatsTransit.Abstractions;

public interface INatsAdapterFactory
{
    IOutboundAdapter<T> OutboundAdapter<T>();
    IInboundAdapter<T> InboundAdapter<T>();
}
