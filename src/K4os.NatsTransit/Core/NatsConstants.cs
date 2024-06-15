namespace K4os.NatsTransit.Core;

public class NatsConstants
{
    public static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(30);
    
    public const string ReplyToHeaderName = "X-ReplyTo";
    public const string ErrorHeaderName = "X-Error";
}
