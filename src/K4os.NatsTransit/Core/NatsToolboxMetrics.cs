using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace K4os.NatsTransit.Core;

public class NatsToolboxMetrics
{
    public static readonly Meter Meter = new("K4os.NatsTransit");

    private static readonly Counter<long> MessagesReceived =
        Meter.CreateCounter<long>("k4os_natstransit_messages_rcvd");

    private static readonly Counter<long> MessagesSent =
        Meter.CreateCounter<long>("k4os_natstransit_messages_sent");

    private static readonly Histogram<double> HandlerDuration =
        Meter.CreateHistogram<double>("k4os_natstransit_handler_duration", "ms");
    
    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("k4os_natstransit_request_duration", "ms");
    
    private long Started() => Stopwatch.GetTimestamp();
    private TimeSpan Elapsed(long start) => Stopwatch.GetElapsedTime(start);

    public void MessageSent(string subject)
    {
        if (!MessagesSent.Enabled) return;

        MessagesSent.Add(1, Tag("subject", subject));
    }

    public void MessageReceived(string subject)
    {
        if (!MessagesReceived.Enabled) return;

        MessagesReceived.Add(1, Tag("subject", subject));
    }

    public void MessageHandled(TimeSpan elapsed, string subject)
    {
        if (!HandlerDuration.Enabled) return;

        HandlerDuration.Record(elapsed.TotalMilliseconds, Tag("subject", subject));
    }

    public void HandlingFailed(TimeSpan elapsed, string subject, Exception error)
    {
        if (!HandlerDuration.Enabled) return;

        HandlerDuration.Record(elapsed.TotalMilliseconds, Tag("subject", subject), Tag(error));
    }
    
    private void ResponseReceived(TimeSpan elapsed, string subject)
    {
        if (!RequestDuration.Enabled) return;

        RequestDuration.Record(elapsed.TotalMilliseconds, Tag("subject", subject));
    }
    
    private void RequestFailed(TimeSpan elapsed, string subject, Exception error)
    {
        if (!RequestDuration.Enabled) return;

        RequestDuration.Record(elapsed.TotalMilliseconds, Tag("subject", subject), Tag(error));
    }
    
    private static KeyValuePair<string, object?> Tag(string key, object? value) =>
        new(key, value);
    
    private static KeyValuePair<string, object?> Tag(Exception ex) =>
        new("error", ex.GetType().Name);

    public async Task<TResponse> HandleScope<TResponse>(string subject, Func<Task<TResponse>> func)
    {
        var timer = Started();
        MessageReceived(subject);
        try
        {
            var response = await func();
            MessageHandled(Elapsed(timer), subject);
            return response;
        }
        catch (Exception ex)
        {
            HandlingFailed(Elapsed(timer), subject, ex);
            throw;
        }
    }

    public async Task<TResponse> RequestScope<TResponse>(string subject, Func<Task<TResponse>> func)
    {
        var timer = Started();
        MessageSent(subject);
        try
        {
            var response = await func();
            ResponseReceived(Elapsed(timer), subject);
            return response;
        }
        catch (Exception ex)
        {
            RequestFailed(Elapsed(timer), subject, ex);
            throw;
        }
    }
}
