namespace K4os.NatsTransit.Extensions;

public static class AsyncExtensions
{
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
}
