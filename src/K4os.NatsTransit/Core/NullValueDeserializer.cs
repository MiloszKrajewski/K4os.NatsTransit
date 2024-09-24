using System.Buffers;
using NATS.Client.Core;

namespace K4os.NatsTransit.Core;

internal class NullValueDeserializer: INatsDeserialize<object?>
{
    public static NullValueDeserializer Default { get; } = new();

    public object? Deserialize(in ReadOnlySequence<byte> buffer) => null;
}
