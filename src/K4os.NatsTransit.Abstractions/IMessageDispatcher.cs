namespace K4os.NatsTransit.Abstractions;

public interface IMessageDispatcher
{
    public Task<object?> Dispatch(object message, CancellationToken token);
}