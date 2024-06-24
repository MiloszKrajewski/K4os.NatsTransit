namespace K4os.NatsTransit.Abstractions;

public interface IMessageBus
{
    Task<object?> Dispatch(
        object message, 
        CancellationToken token = default);
    
    Task<object?> Await(
        Func<object, bool> predicate, 
        TimeSpan? timeout = null, 
        CancellationToken token = default);
}
