using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.KeyValueStore;

namespace K4os.NatsTransit.Locks;

public class NatsDistributedLocks: IDistributedLocks, IDisposable
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaximumInterval = TimeSpan.FromSeconds(1);

    protected readonly ILogger Log;

    private static readonly TimeSpan ExpirationTime = TimeSpan.FromMinutes(1);

    private readonly Guid _storeId;
    private long _sequence;
    
    private readonly INatsKVStore _store;
    private readonly LockStoreObserver _observer;
    private readonly TimeSpan _keepAliveInterval;

    public NatsDistributedLocks(
        ILoggerFactory loggerFactory,
        INatsKVContext context,
        NetsDistributedLocksConfig options)
    {
        Log = loggerFactory.CreateLogger(GetType());
        
        _storeId = Guid.NewGuid();

        var expirationTime = options.ExpirationTime ?? ExpirationTime;
        _keepAliveInterval = expirationTime.Multiply(0.3).NotLessThan(MinimumInterval);
        
        var storeConfig = new NatsKVConfig(options.StoreName) {
            History = 1,
            MaxAge = expirationTime,
            Storage = NatsKVStorageType.Memory
        };
        _store = CreateStore(context, storeConfig);
        _observer = CreateObserver(_store);
    }

    private static INatsKVStore CreateStore(
        INatsKVContext context, NatsKVConfig storeConfig) =>
        context
            .CreateStoreAsync(storeConfig, CancellationToken.None).AsTask()
            .GetAwaiter().GetResult();
    
    private LockStoreObserver CreateObserver(INatsKVStore store) => new(store);

    private static NatsUtf8PrimitivesSerializer<string> ValueSerializer =>
        NatsUtf8PrimitivesSerializer<string>.Default;
    
    public async Task<IDisposable> Acquire(string key, CancellationToken token)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var payload = $"{_storeId:N}:{sequence:X}";
        var revision = await TryAcquire(_store, key, payload, token);
        return new AcquiredLock(_store, key, payload, revision, _keepAliveInterval);
    }

    private async Task<ulong> TryAcquire(
        INatsKVStore store,
        string key, string payload,
        CancellationToken token)
    {
        var revision = await TryAcquireOnce(store, key, payload, token).ConfigureAwait(false);
        if (revision is not null)
            return revision.Value;

        return await TryAcquireLoop(store, key, payload, token).ConfigureAwait(false);
    }

    private async Task<ulong?> TryAcquireOnce(
        INatsKVStore store, string key, string payload, CancellationToken token)
    {
        return await TryCreate(store, key, payload, token);
    }

    private static async Task<ulong?> TryCreate(
        INatsKVStore store, string key, string payload, CancellationToken token)
    {
        try
        {
            return await store
                .CreateAsync(key, payload, ValueSerializer, token)
                .ConfigureAwait(false);
        }
        catch (NatsKVCreateException)
        {
            return null;
        }
        catch (NatsKVWrongLastRevisionException)
        {
            return null;
        }
    }

    private async Task<ulong> TryAcquireLoop(
        INatsKVStore store, string key, string payload, CancellationToken token)
    {
        var interval = MinimumInterval;
        
        while (true)
        {
            var signal = _observer.WaitFor(key);

            var revision = await TryAcquireOnce(store, key, payload, token).ConfigureAwait(false);
            if (revision is not null)
                return revision.Value;

            await signal.WaitAtMost(interval, token).ConfigureAwait(false);

            interval = interval.Multiply(1.5).NotMoreThan(MaximumInterval);
        }
    }

    public void Dispose()
    {
        _observer.Dispose();
    }
}
