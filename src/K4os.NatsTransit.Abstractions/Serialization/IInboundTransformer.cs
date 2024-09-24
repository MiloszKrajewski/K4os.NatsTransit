using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions.Serialization;

public interface IInboundTransformer<in TPayload, out TMessage>
{
    public TMessage Transform(string subject, NatsHeaders? headers, TPayload payload);
}

public class NullInboundTransformer<TMessage>: IInboundTransformer<TMessage, TMessage>
{
    public static NullInboundTransformer<TMessage> Default { get; } = new();
    
    public TMessage Transform(string subject, NatsHeaders? headers, TMessage payload) => payload;
}