using System.Buffers;
using K4os.NatsTransit.Abstractions;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

public interface INatsTargetHandler
{
    Task<object?> Handle(CancellationToken token, object message);
}

public abstract class NatsTargetHandler<TRequest, TResponse>: INatsTargetHandler
{
    protected INatsSerialize<IBufferWriter<byte>> BinarySerializer => 
        NatsRawSerializer<IBufferWriter<byte>>.Default;
    
    protected INatsDeserialize<IMemoryOwner<byte>> BinaryDeserializer => 
        NatsRawSerializer<IMemoryOwner<byte>>.Default;
    
    protected NullOutboundAdapter<TRequest> NullRequestAdapter => 
        NullOutboundAdapter<TRequest>.Default;
    
    protected NullInboundAdapter<TResponse> NullResponseAdapter => 
        NullInboundAdapter<TResponse>.Default;

    public abstract Task<TResponse?> Handle(CancellationToken token, TRequest request);

    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message) => 
        await Handle(token, (TRequest)message);
}

public abstract class NatsTargetHandler<TMessage>: INatsTargetHandler
{
    protected INatsSerialize<IBufferWriter<byte>> BinarySerializer => 
        NatsRawSerializer<IBufferWriter<byte>>.Default;
    
    protected NullOutboundAdapter<TMessage> NullAdapter => 
        NullOutboundAdapter<TMessage>.Default;

    public abstract Task Handle(CancellationToken token, TMessage message);

    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message)
    {
        await Handle(token, (TMessage)message);
        return default;
    }
}
