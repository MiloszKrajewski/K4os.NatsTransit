using System.Diagnostics;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Internal;
using K4os.NatsTransit.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Serilog;

var services = new ServiceCollection();
services.AddSerilog(logging => logging.WriteTo.Console());

services.AddSingleton<NatsConnection>();
services.AddSingleton<NatsJSContext>();
services.AddSingleton<NatsKVContext>();

services.AddSingleton<IDistributedLocks>(
    p => new NatsDistributedLocks(
        p.GetRequiredService<ILoggerFactory>(), 
        p.GetRequiredService<NatsKVContext>(),
        new NetsDistributionLocksConfig {
            StoreName = "locks2",
        }));

var provider = services.BuildServiceProvider();

var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DeepTrace");
DeepTrace.Enable(s => log.LogInformation("{DeepTrace}", s));
DeepTrace.Register<AcquiredLock>();

var locks = provider.GetRequiredService<IDistributedLocks>();
long concurrent = 0;

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
    Log.Information("Oops!?");
}
finally
{
    cts.Cancel();
}

async Task LockLoop(string key)
{
    var counter = 0;
    var started = Stopwatch.GetTimestamp();
    while (true)
    {
        cts.Token.ThrowIfCancellationRequested();
        
        var l = await locks.Acquire(key, TimeSpan.FromSeconds(5), CancellationToken.None);
        
        if (Interlocked.Increment(ref concurrent) != 1)
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
        }
        await Task.Delay(1);
        Interlocked.Decrement(ref concurrent);
        l.Dispose();
        counter++;
        if (counter % 100 != 0) continue;

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Log.Information("{Elapsed:0.0} ms/lock", elapsed / counter);
    }
}