namespace K4os.NatsTransit.Abstractions.MessageBus;

public interface IMessageDispatcher
{
    public Task<object?> Dispatch(object message, CancellationToken token);
}