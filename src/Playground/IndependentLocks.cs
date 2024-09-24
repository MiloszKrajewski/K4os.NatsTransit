using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.DistributedLocks;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Playground;

[SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
internal class IndependentLocks
{
    private readonly ILogger _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly IDistributedLocks _locks;
    
    public IndependentLocks(ILoggerFactory loggerFactory, IDistributedLocks locks)
    {
        _log = loggerFactory.CreateLogger<CompetingLocks>();
        _locks = locks;
    }

    public async Task Run()
    {
        CancellationTokenSource cts = new();

        try
        {
            await Task.WhenAll(
                LockLoop("lock2"),
                LockLoop("lock3"),
                LockLoop("lock4")
            );
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _log.LogInformation("Oops!?");
        }
        finally
        {
            cts.Cancel();
        }

    }
    
    [SuppressMessage("ReSharper", "FunctionNeverReturns")]
    async Task LockLoop(string key)
    {
        var counter = 0;
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            _cts.Token.ThrowIfCancellationRequested();
        
            var l = await _locks.Acquire(key, TimeSpan.FromSeconds(5), CancellationToken.None);
            // await Task.Delay(1);
            l.Dispose();
            counter++;
            if (counter % 100 != 0) continue;

            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Log.Information("{Elapsed:0.0} ms/lock", elapsed / counter);
        }
    }
}