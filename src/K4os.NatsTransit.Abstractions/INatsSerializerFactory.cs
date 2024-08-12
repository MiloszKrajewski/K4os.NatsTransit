using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions;

public interface INatsSerializerXFactory
{
    INatsSerialize<T>? PayloadSerializer<T>();
    INatsDeserialize<T>? PayloadDeserializer<T>();
    IInboundAdapter<T>? InboundAdapter<T>();
    IOutboundAdapter<T>? OutboundAdapter<T>();
    IExceptionSerializer? ExceptionSerializer();
}