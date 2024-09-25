namespace K4os.NatsTransit.Abstractions.Serialization;

public interface ICustomSerializer<in TMessage>: IOutboundTransformer<TMessage, Memory<byte>>;
