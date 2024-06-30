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

    public void Inject(ref NatsHeaders? headers)
    {
        headers ??= new NatsHeaders();
        var activityContext = Activity.Current?.Context ?? default;
        var propagationContext = new PropagationContext(activityContext, Baggage.Current);
        Propagator.Inject(propagationContext, headers, static (h, k, v) => Inject(h, k, v));
    }

    public void Extract(NatsHeaders? headers)
    {
        if (headers is null) return;
        var context = Propagator.Extract(default, headers, static (h, k) => Extract(h, k));
        Baggage.Current = context.Baggage;
    }

    private static void Inject(NatsHeaders h, string k, string v) => 
        h[k] = v;
    
    private static StringValues Extract(NatsHeaders h, string k) => 
        h.TryGetValue(k, out var v) ? v : [];
}
