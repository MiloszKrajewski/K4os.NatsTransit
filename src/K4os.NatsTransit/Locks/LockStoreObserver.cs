using System.Collections.Concurrent;
using K4os.Async.Toys;
using K4os.NatsTransit.Core;
using NATS.Client.KeyValueStore;

namespace K4os.NatsTransit.Locks;

public class LockStoreObserver: IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _locks = new();
    private readonly Agent _agent;

    public LockStoreObserver(INatsKVStore store)
    {
        _cts = new CancellationTokenSource();
        _agent = Agent.Create(c => Loop(store, c.Token), null, _cts.Token);
    }

    private async Task Loop(INatsKVStore store, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var stream = Observe(store, token);
        while (await stream.MoveNextAsync())
        {
            var entry = stream.Current;
            if (entry.Operation != NatsKVOperation.Purge) continue;

            PurgeOne(entry.Key);
        }
    }

    private static IAsyncEnumerator<NatsKVEntry<object?>> Observe(
        INatsKVStore store, CancellationToken token)
    {
        var options = new NatsKVWatchOpts {
            MetaOnly = true,
            UpdatesOnly = true,
        };
        return store
            .WatchAsync(NullValueDeserializer.Default, options, token)
            .GetAsyncEnumerator(token);
    }

    private void PurgeOne(string key)
    {
        if (_locks.TryRemove(key, out var tcs))
            tcs.TrySetResult();
    }

    private void PurgeAll()
    {
        // it is not about purging them all, but to purge the ones
        // which are in dictionary at the time of calling this method
        // all new ones will start cancelled immediately after creation
        foreach (var key in _locks.Keys.ToArray())
        {
            if (_locks.TryRemove(key, out var tcs))
                tcs.TrySetCanceled();
        }
    }

    public Task WaitFor(string key)
    {
        _cts.Token.ThrowIfCancellationRequested();
        var added = _locks.AddOrUpdate(key, _ => new TaskCompletionSource(), (_, tcs) => tcs);

        // most of the time we return here,
        // we have a completion source, and it has not been cancelled - end of story
        if (!_cts.IsCancellationRequested)
            return added.Task;

        // ok, but it seems it has been cancelled in the meantime
        added.TrySetCanceled();

        // we need to remove it from the dictionary,
        // but we cannot 100% guarantee that it was
        // not replaced in the meantime
        if (!_locks.TryRemove(key, out var removed))
            return added.Task;

        // most likely added == removed but not guaranteed
        removed.TrySetCanceled();
        return removed.Task;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _agent.Stop().Wait();
        PurgeAll();
        _cts.Dispose();
    }
}
