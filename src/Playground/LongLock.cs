using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.DistributedLocks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Playground;

[SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
internal class LongLock
{
    private readonly ILogger _log;
    private readonly IDistributedLocks _locks;
    private readonly TaskCompletionSource _locked = new();
    private readonly TaskCompletionSource _released = new();
    
    public LongLock(ILoggerFactory loggerFactory, IDistributedLocks locks)
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
                LockForLock("lock8"),
                TryLockInTheMeantime("lock8")
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

    private async Task LockForLock(string key)
    {
        using var l = await _locks.Acquire(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        _locked.TrySetResult();

        for (var i = 60; i > 0; i--)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            _log.LogInformation("Lock is held for {Left} more seconds", i);
        }
        
        _released.TrySetResult();
    }
    
    private async Task TryLockInTheMeantime(string key)
    {
        await _locked.Task;
        
        bool IsReleased() => _released.Task.IsCompleted;

        while (!IsReleased())
        {
            try
            {
                using var inner = await _locks.Acquire(
                    key, TimeSpan.FromSeconds(5), CancellationToken.None);
                if (!IsReleased())
                    throw new Exception("Lock was acquired while it should not be");
                
            }
            catch (TimeoutException e)
            {
                _log.LogWarning(e, "Lock was not acquired");
            }
        }
        
        using var outer = await _locks.Acquire(
            key, TimeSpan.FromSeconds(5), CancellationToken.None);
        _log.LogInformation("Lock was finally acquired");
    }
}