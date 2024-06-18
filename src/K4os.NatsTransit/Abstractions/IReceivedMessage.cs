namespace K4os.NatsTransit.Abstractions;

public interface IReceivedMessage
{
    string Subject { get; }
    string? ReplyTo { get; }
    object? Payload { get; }
    Exception? Exception { get; }

    ValueTask Complete(CancellationToken token = default);
    ValueTask Forget(CancellationToken token = default);
    ValueTask KeepAlive(CancellationToken token = default);
}
