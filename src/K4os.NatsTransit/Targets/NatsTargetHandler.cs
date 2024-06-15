namespace K4os.NatsTransit.Targets;

public interface INatsTargetHandler
{
    Task<object?> Handle(CancellationToken token, object message);
}

public abstract class NatsTargetHandler<TRequest, TResponse>: INatsTargetHandler
{
    public abstract Task<TResponse?> Handle(CancellationToken token, TRequest request);

    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message) => 
        await Handle(token, (TRequest)message);
}

public abstract class NatsTargetHandler<TMessage>: INatsTargetHandler
{
    public abstract Task Handle(CancellationToken token, TMessage message);

    async Task<object?> INatsTargetHandler.Handle(CancellationToken token, object message)
    {
        await Handle(token, (TMessage)message);
        return default;
    }
}
