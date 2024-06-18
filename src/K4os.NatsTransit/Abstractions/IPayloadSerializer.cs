using System.Buffers;

namespace K4os.NatsTransit.Abstractions;

public interface IPayloadSerializer
{
    void Serialize(object? payload, IBufferWriter<byte> writer);
    object? Deserialize(Type typeHint, ReadOnlySequence<byte> payload);
}