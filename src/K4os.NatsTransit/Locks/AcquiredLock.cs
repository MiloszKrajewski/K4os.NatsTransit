using K4os.NatsTransit.Extensions;
using NATS.Client.Core;
using NATS.Client.KeyValueStore;

namespace K4os.NatsTransit.Locks;

public class AcquiredLock: IDisposable
{
    private readonly CancellationTokenSource _cts;

    public AcquiredLock(
        INatsKVStore store,
        string key, string payload, ulong revision,
        TimeSpan interval)
    {
        _cts = new CancellationTokenSource();
        _ = KeepAlive(store, key, payload, revision, interval, _cts.Token);
    }

    private static async Task KeepAlive(
        INatsKVStore store,
        string key, string payload, ulong revision,
        TimeSpan interval, CancellationToken token)
    {
        var forever = AsyncExtensions.NeverCompletedTask;
        var sequence = 0;

        try
        {
            await forever.KeepAlive(
                async _ => revision = await Refresh(store, key, payload, ++sequence),
                interval, token);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            await Release(store, key, revision);
        }
    }

    private static Task<ulong> Refresh(
        INatsKVStore store, string key, string payload, int sequence) =>
        store.PutAsync(
            key, $"{payload}:{sequence}",
            NatsUtf8PrimitivesSerializer<string>.Default,
            CancellationToken.None
        ).AsTask();

    private static Task Release(
        INatsKVStore store, string key, ulong revision) =>
        store.PurgeAsync(
            key,
            new NatsKVDeleteOpts { Purge = true, Revision = revision },
            CancellationToken.None
        ).AsTask();

    public void Dispose()
    {
        _cts.Cancel();
    }
}
