using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions.Serialization;

public interface IOutboundTransformer<in TMessage, out TPayload>
{
    public TPayload Adapt(string subject, ref NatsHeaders? headers, TMessage payload);
}

public class NullOutboundTransformer<TMessage>: IOutboundTransformer<TMessage, TMessage>
{
    public static NullOutboundTransformer<TMessage> Default { get; } = new();
    
    public TMessage Adapt(string subject, ref NatsHeaders? headers, TMessage payload) => 
        payload;
}