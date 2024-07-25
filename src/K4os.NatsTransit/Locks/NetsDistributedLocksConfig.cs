namespace K4os.NatsTransit.Locks;

public class NetsDistributedLocksConfig
{
    public string StoreName { get; set; } = "locks";
    public TimeSpan? ExpirationTime { get; set; }
}
