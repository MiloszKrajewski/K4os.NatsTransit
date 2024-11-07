namespace K4os.NatsTransit.Abstractions.DistributedLocks;

/// <summary>
/// Abstraction of distributed locks.
/// </summary>
public interface IDistributedLocks
{
    /// <summary>Acquire as lock.</summary>
    /// <param name="key">Key.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Acquired lock handle, which can be disposed.</returns>
    /// <exception cref="TimeoutException">If lock cannot be acquired within timeout.</exception>
    public Task<IDisposable> Acquire(string key, CancellationToken token);
}

/// <summary>
/// Distributed locks extensions.
/// </summary>
public static class DistributedLocksExtensions
{
    /// <summary>Acquire lock with timeout.</summary>
    /// <param name="locks"><see cref="IDistributedLocks"/> provider.</param>
    /// <param name="key">Key.</param>
    /// <param name="timeout">Timeout.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Acquired lock handle, which can be disposed.</returns>
    /// <exception cref="TimeoutException">If lock cannot be acquired within timeout.</exception>
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
    
    /// <summary>Acquire lock with timeout.</summary>
    /// <param name="locks"><see cref="IDistributedLocks"/> provider.</param>
    /// <param name="key">Key.</param>
    /// <returns>Acquired lock handle, which can be disposed.</returns>
    /// <remarks>Please note: trying to acquire lock without timeout is a little bit dangerous,
    /// as it may halt application for a long time</remarks>
    public static Task<IDisposable> Acquire(
        this IDistributedLocks locks, string key) =>
        locks.Acquire(key, CancellationToken.None);
    
    /// <summary>Acquire lock with timeout.</summary>
    /// <param name="locks"><see cref="IDistributedLocks"/> provider.</param>
    /// <param name="key">Key.</param>
    /// <param name="timeout">Timeout.</param>
    /// <returns>Acquired lock handle, which can be disposed.</returns>
    /// <exception cref="TimeoutException">If lock cannot be acquired within timeout.</exception>
    public static Task<IDisposable> Acquire(
        this IDistributedLocks locks, string key, TimeSpan timeout) =>
        locks.Acquire(key, timeout, CancellationToken.None);
}
