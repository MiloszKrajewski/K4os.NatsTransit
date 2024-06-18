using K4os.NatsTransit.Abstractions;
using NATS.Client.Core;

namespace K4os.NatsTransit.Core.Messages;

public class CoreNatsReceivedMessage<T>: IReceivedMessage
{
    private readonly NatsMsg<T> _message;
    
    private readonly string? _replyTo;
    private readonly object? _payload;
    private readonly Exception? _exception;
    
    public string Subject => _message.Subject;
    public string? ReplyTo => _replyTo;
    public object? Payload => _payload;
    public Exception? Exception => _exception;

    public CoreNatsReceivedMessage(
        NatsMsg<T> message, object? payload, Exception? exception)
    {
        exception ??= message.Error;
        _message = message;
        _replyTo = message.Headers?.GetReplyToHeader() ?? message.ReplyTo;
        _payload = exception is null ? payload : null;
        _exception = exception;
    }

    public ValueTask Complete(CancellationToken token = default) => default;
    public ValueTask Forget(CancellationToken token = default) => default;
    public ValueTask KeepAlive(CancellationToken token = default) => default;
}