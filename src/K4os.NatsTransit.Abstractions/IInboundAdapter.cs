using System.Buffers;
using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions;

public interface IInboundAdapter<in TPayload, out TMessage>
{
    public TMessage Adapt(
        string subject, 
        NatsHeaders? headers, 
        TPayload payload);
}

public interface IInboundAdapter<out TMessage>: IInboundAdapter<IMemoryOwner<byte>, TMessage>;

public class NullInboundAdapter<TMessage>: IInboundAdapter<TMessage, TMessage>
{
    public static NullInboundAdapter<TMessage> Default { get; } = new();
    
    public TMessage Adapt(string subject, NatsHeaders? headers, TMessage payload) => payload;
}