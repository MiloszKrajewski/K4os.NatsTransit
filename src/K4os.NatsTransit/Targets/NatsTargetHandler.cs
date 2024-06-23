using System.Buffers;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

public interface INatsTargetHandler
{
    bool CanHandleType(Type type);
    bool CanHandle(Type? type, object message);
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
    public virtual bool CanHandle(TRequest request) => true;

    public bool CanHandleType(Type type) => type.InheritsFrom<TRequest>();

    public bool CanHandle(Type? type, object message) => 
        (type is null || CanHandleType(type)) && CanHandle((TRequest)message);

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

    public bool CanHandleType(Type type) => type.InheritsFrom<TRequest>();

    public bool CanHandle(Type? type, object message) => 
        (type is null || CanHandleType(type)) && CanHandle((TRequest)message);

    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message)
    {
        await Handle(token, (TRequest)message);
        return null;
    }
}
