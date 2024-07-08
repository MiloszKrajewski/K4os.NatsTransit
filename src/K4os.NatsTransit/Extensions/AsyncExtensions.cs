namespace K4os.NatsTransit.Extensions;

public static class AsyncExtensions
{
    public static readonly Task NeverCompletedTask = new TaskCompletionSource().Task;

    public static Task KeepAlive<TTask>(
        this Task response,
        Func<CancellationToken, TTask> keepAlive,
        TimeSpan keepAliveInterval,
        CancellationToken token)
        where TTask: Task =>
        response.KeepAlive(t => new ValueTask(keepAlive(t)), keepAliveInterval, token);

    public static async Task KeepAlive(
        this Task response,
        Func<CancellationToken, ValueTask> keepAlive,
        TimeSpan keepAliveInterval,
        CancellationToken token)
    {
        if (response.IsCompleted)
            return;

        using var timer = new PeriodicTimer(keepAliveInterval);
        while (true)
        {
            var nextTick = timer.WaitForNextTickAsync(token).AsTask();
            var interval = await Task.WhenAny(response, nextTick);

            if (interval == response)
                return;

            token.ThrowIfCancellationRequested();

            await keepAlive(token);
        }
    }

    public static async Task<T?> FirstOrDefault<T>(
        this IAsyncEnumerable<T> subscription, CancellationToken token)
    {
        await using var enumerator = subscription.GetAsyncEnumerator(token);
        var next = await enumerator.MoveNextAsync();
        token.ThrowIfCancellationRequested();
        return next ? enumerator.Current : default;
    }

    public static async Task<Task?> WaitAtMost(
        this Task task, TimeSpan timeout, CancellationToken token)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var delay = Task.Delay(timeout, token);
        try
        {
            var done = await Task.WhenAny(task, delay);
            return done == task || task.IsCompleted ? task : null;
        }
        finally
        {
            // ReSharper disable once MethodHasAsyncOverload
            cts.Cancel();
            cts.Dispose();
        }
    }

    public static async Task<Task<T>?> WaitAtMost<T>(
        this Task<T> task, TimeSpan timeout, CancellationToken token)
    {
        var done = await ((Task)task).WaitAtMost(timeout, token);
        return done is null ? null : task;
    }

    public static CancellationTokenSource WithTimeout(
        this CancellationToken token, TimeSpan timeout)
    {
        var hasTimeout =
            timeout != Timeout.InfiniteTimeSpan &&
            timeout >= TimeSpan.Zero && timeout < TimeSpan.MaxValue;
        var canBeCanceled = token.CanBeCanceled;
        var cts =
            !hasTimeout && !canBeCanceled ? new CancellationTokenSource() :
            !canBeCanceled ? new CancellationTokenSource(timeout) :
            CancellationTokenSource.CreateLinkedTokenSource(token);
        if (hasTimeout && canBeCanceled)
            cts.CancelAfter(timeout);
        return cts;
    }
}
