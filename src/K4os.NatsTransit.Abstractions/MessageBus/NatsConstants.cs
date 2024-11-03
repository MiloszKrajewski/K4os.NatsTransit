namespace K4os.NatsTransit.Abstractions.MessageBus;

public class NatsConstants
{
    public static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(30);
    
    public const string ReplyToHeaderName = "X-ReplyTo";
    public const string KnownTypeHeaderName = "X-KnownType";
    public const string ErrorHeaderName = "X-Error";
}
