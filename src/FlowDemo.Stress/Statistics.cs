using System.Diagnostics;
using K4os.NatsTransit.Extensions;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Stress;

public static class Statistics
{
    private static long _firstRcvd;
    private static long _firstSent;
    
    private static long _lastRcvd;
    private static long _lastSent;
    
    private static long _countRcvd;
    private static long _countSent;

    public static void OnMessageSent()
    {
        Interlocked.Increment(ref _countSent);
        var timestamp = Stopwatch.GetTimestamp();
        Interlocked.CompareExchange(ref _firstSent, timestamp, 0);
        Interlocked.Exchange(ref _lastSent, timestamp);
    }

    public static void OnMessageReceived()
    {
        Interlocked.Increment(ref _countRcvd);
        var timestamp = Stopwatch.GetTimestamp();
        Interlocked.CompareExchange(ref _firstRcvd, timestamp, 0);
        Interlocked.Exchange(ref _lastRcvd, timestamp);
    }

    public static Task WaitForSilence(ILogger logger, TimeSpan? timeout = null) =>
        WaitForSilence(logger, timeout ?? TimeSpan.FromSeconds(5));

    private static async Task WaitForSilence(ILogger logger, TimeSpan timeout)
    {
        var started = Stopwatch.GetTimestamp();
        var lastReport = DateTime.MinValue;
        while (true)
        {
            var lastEvent = Interlocked.Read(ref _lastRcvd)
                .NotLessThan(Interlocked.Read(ref _lastSent))
                .NotLessThan(started);
            var elapsed = Stopwatch.GetElapsedTime(lastEvent);
            if (elapsed > timeout) break;
            
            if (DateTime.UtcNow - lastReport > TimeSpan.FromSeconds(1))
            {
                if (elapsed.TotalSeconds > 1)
                    logger.LogWarning("No new events for {Elapsed:0.0} seconds", elapsed.TotalSeconds);
                logger.LogInformation(
                    "Rcvd: {Rcvd} ({RcvdRate:0}/s), Sent: {Sent} ({SentRate:0}/s)",
                    Interlocked.Read(ref _countRcvd), RcvdRate(),
                    Interlocked.Read(ref _countSent), SendRate());
                lastReport = DateTime.UtcNow;
            }

            await Task.Delay(100);
        }
    }

    private static double RcvdRate() => 
        Rate(Interlocked.Read(ref _firstRcvd), Interlocked.Read(ref _lastRcvd), Interlocked.Read(ref _countRcvd));
    
    private static double SendRate() => 
        Rate(Interlocked.Read(ref _firstSent), Interlocked.Read(ref _lastSent), Interlocked.Read(ref _countSent));

    private static double Rate(long first, long last, long count)
    {
        if (last <= first) return 0;
        var elapsed = Stopwatch.GetElapsedTime(first, last).TotalSeconds;
        return count / elapsed;
    }
}
