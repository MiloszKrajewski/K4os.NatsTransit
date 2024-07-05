using System.Buffers;
using K4os.NatsTransit.Abstractions;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

public interface INatsTargetHandler
{
    Type BaseType { get; }
    bool CanHandle(object message);
    Task<object?> Handle(CancellationToken token, object message);
}

public abstract class NatsTargetHandler<TRequest, TResponse>: INatsTargetHandler
{
    protected INatsSerialize<IBufferWriter<byte>> BinarySerializer => 
        NatsRawSerializer<IBufferWriter<byte>>.Default;
    
    protected INatsDeserialize<IMemoryOwner<byte>> BinaryDeserializer => 
        NatsRawSerializer<IMemoryOwner<byte>>.Default;
    
    protected NullOutboundAdapter<TRequest> NullOutboundAdapter => 
        NullOutboundAdapter<TRequest>.Default;
    
    protected NullInboundAdapter<TResponse> NullInboundAdapter => 
        NullInboundAdapter<TResponse>.Default;

    public abstract Task<TResponse?> Handle(CancellationToken token, TRequest request);
    
    public Type BaseType => typeof(TRequest);
    
    public bool CanHandle(object message) => message is TRequest request && CanHandle(request); 

    public virtual bool CanHandle(TRequest request) => true;

    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message) => 
        await Handle(token, (TRequest)message);
}

public abstract class NatsTargetHandler<TRequest>: INatsTargetHandler
{
    protected INatsSerialize<IBufferWriter<byte>> BinarySerializer => 
        NatsRawSerializer<IBufferWriter<byte>>.Default;
    
    protected INatsDeserialize<IMemoryOwner<byte>> BinaryDeserializer => 
        NatsRawSerializer<IMemoryOwner<byte>>.Default;
    
    protected NullOutboundAdapter<TRequest> NullOutboundAdapter => 
        NullOutboundAdapter<TRequest>.Default;
    
    public abstract Task Handle(CancellationToken token, TRequest request);
    
    public virtual bool CanHandle(TRequest request) => true;
    
    public bool CanHandle(object message) => message is TRequest request && CanHandle(request);
    
    public Type BaseType => typeof(TRequest);

    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message)
    {
        await Handle(token, (TRequest)message);
        return null;
    }
}
