using System.Buffers;

namespace K4os.NatsTransit.Abstractions.Serialization;

public interface ICustomDeserializer<out TMessage>: IInboundTransformer<IMemoryOwner<byte>, TMessage>;
