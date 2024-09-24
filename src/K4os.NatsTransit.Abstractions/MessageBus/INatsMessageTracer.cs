using System.Diagnostics;
using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions.MessageBus;

public interface INatsMessageTracer
{
    public void Inject(ActivityContext? context, ref NatsHeaders? headers);
    public ActivityContext? Extract(NatsHeaders? headers);
}

public class NullMessageTracer: INatsMessageTracer
{
    public static NullMessageTracer Instance { get; } = new();

    private NullMessageTracer() { }

    public void Inject(ActivityContext? context, ref NatsHeaders? headers) { }
    
    public ActivityContext? Extract(NatsHeaders? headers) => null;
}
