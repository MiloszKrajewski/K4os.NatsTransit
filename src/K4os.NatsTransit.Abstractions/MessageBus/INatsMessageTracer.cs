using System.Diagnostics;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions.MessageBus;

public interface INatsMessageTracer
{
    public void Inject(ActivityContext? context, ref Dictionary<string, StringValues>? headers);
    public ActivityContext? Extract(IDictionary<string, StringValues>? headers);
}

public class NullMessageTracer: INatsMessageTracer
{
    public static NullMessageTracer Instance { get; } = new();

    private NullMessageTracer() { }

    public void Inject(ActivityContext? context, ref Dictionary<string, StringValues>? headers) { }
    
    public ActivityContext? Extract(IDictionary<string, StringValues>? headers) => null;
}
