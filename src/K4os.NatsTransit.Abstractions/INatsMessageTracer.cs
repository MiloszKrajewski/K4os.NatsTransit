using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions;

public interface INatsMessageTracer
{
    public void Inject(ref NatsHeaders? headers);
    public void Extract(NatsHeaders? headers);
}

public class NullMessageTracer: INatsMessageTracer
{
    public static NullMessageTracer Instance { get; } = new();

    private NullMessageTracer() { }

    public void Inject(ref NatsHeaders? headers) { }
    public void Extract(NatsHeaders? headers) { }
}
