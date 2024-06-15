using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions;

public interface INatsSerializerFactory
{
    INatsSerialize<T> PayloadSerializer<T>();
    INatsDeserialize<T> PayloadDeserializer<T>();
}
