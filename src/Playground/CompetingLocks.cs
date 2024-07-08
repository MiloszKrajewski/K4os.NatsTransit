using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using Microsoft.Extensions.Logging;

namespace Playground;

[SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
internal class CompetingLocks
{
    private readonly ILogger _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly IDistributedLocks _locks;
    
    private long _concurrent = 0;

    public CompetingLocks(ILoggerFactory loggerFactory, IDistributedLocks locks)
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
                LockLoop("lock1"),
                LockLoop("lock1"),
                LockLoop("lock1")
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
    
    async Task LockLoop(string key)
    {
        var counter = 0;
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            _cts.Token.ThrowIfCancellationRequested();
        
            var l = await _locks.Acquire(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        
            if (Interlocked.Increment(ref _concurrent) != 1)
            {
                _cts.Cancel();
                _cts.Token.ThrowIfCancellationRequested();
            }
            await Task.Delay(1);
            Interlocked.Decrement(ref _concurrent);
            l.Dispose();
            counter++;
            if (counter % 100 != 0) continue;

            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Serilog.Log.Information("{Elapsed:0.0} ms/lock", elapsed / counter);
        }
    }
}