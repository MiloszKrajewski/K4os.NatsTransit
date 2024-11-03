using System.Diagnostics;
using K4os.NatsTransit.Abstractions.MessageBus;
using Microsoft.Extensions.Primitives;

namespace K4os.NatsTransit.Core;

public class NatsToolboxTracing(INatsMessageTracer tracer)
{
    public static readonly ActivitySource ActivitySource = new("K4os.NatsTransit");

    public Activity? SendingScope(string activityName, bool awaitsResponse) =>
        ActivitySource.StartActivity(
            activityName,
            awaitsResponse ? ActivityKind.Client : ActivityKind.Producer);

    public Activity? ReceivedScope(
        string activityName, IDictionary<string, StringValues>? headers, bool hasResponse) =>
        ActivitySource.StartActivity(
            activityName,
            hasResponse ? ActivityKind.Server : ActivityKind.Consumer,
            tracer.Extract(headers) ?? default);

    internal void TryAddTrace(ref Dictionary<string, StringValues>? headers) =>
        tracer.Inject(Activity.Current?.Context, ref headers);
}
