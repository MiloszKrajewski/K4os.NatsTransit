using System.Buffers;
using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions;

public interface IOutboundAdapter<in TMessage, out TPayload>
{
    public TPayload Adapt(string subject, ref NatsHeaders? headers, TMessage payload);
}

public interface IOutboundAdapter<in TMessage>: IOutboundAdapter<TMessage, IBufferWriter<byte>>;

public class NullOutboundAdapter<TMessage>: IOutboundAdapter<TMessage, TMessage>
{
    public static NullOutboundAdapter<TMessage> Default { get; } = new();
    
    public TMessage Adapt(string subject, ref NatsHeaders? headers, TMessage payload) => 
        payload;
}
