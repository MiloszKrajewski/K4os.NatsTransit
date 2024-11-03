namespace K4os.NatsTransit.Patterns;

public interface INatsTargetHandler
{
    Type BaseType { get; }
    bool CanHandle(object message);
    Task<object?> Handle(CancellationToken token, object message);
}

public abstract class NatsTargetHandler<TRequest>: INatsTargetHandler
{
    public Type BaseType => typeof(TRequest);
    
    public bool CanHandle(object message) => message is TRequest request && CanHandle(request);
    public virtual bool CanHandle(TRequest request) => true;

    public abstract Task Handle(CancellationToken token, TRequest request);
   
    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message)
    {
        await Handle(token, (TRequest)message);
        return null;
    }
}

public abstract class NatsTargetHandler<TRequest, TResponse>: INatsTargetHandler
{
    public Type BaseType => typeof(TRequest);

    public bool CanHandle(object message) => message is TRequest request && CanHandle(request); 
    public virtual bool CanHandle(TRequest request) => true;
    
    public abstract Task<TResponse> Handle(CancellationToken token, TRequest request);
   
    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message) => 
        await Handle(token, (TRequest)message);
}
