using K4os.NatsTransit.Abstractions.DistributedLocks;
using K4os.NatsTransit.Internal;
using K4os.NatsTransit.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Playground;
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
        new NetsDistributedLocksConfig {
            StoreName = "locks2",
        }));

var provider = services.BuildServiceProvider();

var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DeepTrace");
DeepTrace.Enable(s => log.LogInformation("{DeepTrace}", s));
DeepTrace.Register<AcquiredLock>();

var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
var locks = provider.GetRequiredService<IDistributedLocks>();

var demo1 = new LongLock(loggerFactory, locks);
await demo1.Run();
