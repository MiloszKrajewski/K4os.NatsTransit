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
        foreach (var tcs in _locks.Values)
            tcs.TrySetCanceled();
        _locks.Clear();
    }

    public Task WaitFor(string key)
    {
        _cts.Token.ThrowIfCancellationRequested();
        return _locks.AddOrUpdate(key, _ => new TaskCompletionSource(), (_, tcs) => tcs).Task;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _agent.Stop().Wait();
        PurgeAll();
        _cts.Dispose();
    }
}
