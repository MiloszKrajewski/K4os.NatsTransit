namespace K4os.NatsTransit.Abstractions.DistributedLocks;

public interface IDistributedLocks
{
    public Task<IDisposable> Acquire(string key, CancellationToken token);
}

public static class DistributedLocksExtensions
{
    public static async Task<IDisposable> Acquire(
        this IDistributedLocks locks, string key, TimeSpan timeout, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(timeout);
        
        try
        {
            return await locks.Acquire(key, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"Failed to acquire lock {key} within {timeout}");
        }
        finally
        {
            // ReSharper disable once MethodHasAsyncOverload
            cts.Cancel();
        }
    }
    
    public static Task<IDisposable> Acquire(
        this IDistributedLocks locks, string key) =>
        locks.Acquire(key, CancellationToken.None);
    
    public static Task<IDisposable> Acquire(
        this IDistributedLocks locks, string key, TimeSpan timeout) =>
        locks.Acquire(key, timeout, CancellationToken.None);
}
