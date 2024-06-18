using K4os.NatsTransit.Abstractions;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core.Messages;

public class JetStreamReceivedMessage<T>: IReceivedMessage
{
    private readonly NatsJSMsg<T> _message;
    private readonly string? _replyTo;
    private readonly object? _payload;
    private readonly Exception? _exception;

    public string Subject => _message.Subject;
    public string? ReplyTo => _replyTo;
    public object? Payload => _payload;
    public Exception? Exception => _exception;

    public JetStreamReceivedMessage(
        NatsJSMsg<T> message, object? payload, Exception? exception)
    {
        exception ??= message.Error;
        _message = message;
        _replyTo = message.Headers?.GetReplyToHeader() ?? message.ReplyTo;
        _payload = exception is null ? payload : null;
        _exception = exception;
    }

    public ValueTask Complete(CancellationToken token = default) =>
        _message.AckAsync(default, token);

    public ValueTask Forget(CancellationToken token = default) =>
        _message.AckTerminateAsync(default, token);

    public ValueTask KeepAlive(CancellationToken token = default) =>
        _message.AckProgressAsync(default, token);
}
