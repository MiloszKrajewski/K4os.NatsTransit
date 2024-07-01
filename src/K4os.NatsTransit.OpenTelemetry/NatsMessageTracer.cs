using System.Diagnostics;
using K4os.NatsTransit.Abstractions;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace K4os.NatsTransit.OpenTelemetry;

public class NatsMessageTracer: INatsMessageTracer
{
    private TextMapPropagator Propagator => Propagators.DefaultTextMapPropagator;

    public void Inject(ActivityContext? context, ref NatsHeaders? headers)
    {
        if (context is null) return;
        headers ??= new NatsHeaders();
        var propagationContext = new PropagationContext(context.Value, Baggage.Current); 
        Propagator.Inject(propagationContext, headers, static (h, k, v) => Inject(h, k, v));
    }

    public ActivityContext? Extract(NatsHeaders? headers)
    {
        if (headers is null) return null;
        var context = Propagator.Extract(default, headers, static (h, k) => Extract(h, k));
        Baggage.Current = context.Baggage;
        return context.ActivityContext;
    }

    private static void Inject(NatsHeaders h, string k, string v) => 
        h[k] = v;
    
    private static StringValues Extract(NatsHeaders h, string k) => 
        h.TryGetValue(k, out var v) ? v : [];
}
