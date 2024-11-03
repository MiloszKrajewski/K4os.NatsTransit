using System.Text.Json;
using FlowDemo.Hosting.Extensions;
using FlowDemo.Stress;
using FlowDemo.Stress.Handlers;
using FlowDemo.Stress.Messages;
using K4os.KnownTypes;
using K4os.KnownTypes.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Serilog;
using Serilog.Events;

var services = new ServiceCollection();
services.AddSerilog(
    logging => logging
        .WriteTo.Console()
        .MinimumLevel.Information()
        .MinimumLevel.Override("FlowDemo.Hosting.Services", LogEventLevel.Warning));

var typesRegistry = new KnownTypesRegistry();
typesRegistry.RegisterAssembly<Program>();

services.AddSingleton(typesRegistry);
services.AddSingleton<JsonSerializerOptions>(
    _ => new JsonSerializerOptions { TypeInfoResolver = typesRegistry.CreateJsonTypeInfoResolver() });
services.AddMediatR(m => m.RegisterServicesFromAssemblyContaining<Program>());

services.AddSingleton<NatsOpts>(
    p => new NatsOpts {
        Url = "nats://localhost:4222",
        Name = "FlowDemo.Stress",
        LoggerFactory = p.GetRequiredService<ILoggerFactory>()
    });
services.AddSingleton<NatsJSOpts>();
services.AddSingleton<NatsConnection>();
services.AddSingleton<NatsJSContext>();
services.AddSingleton<INatsConnection>(p => p.GetRequiredService<NatsConnection>());
services.AddSingleton<INatsJSContext>(p => p.GetRequiredService<NatsJSContext>());

services.ConfigureMessageBus(
    c => c
        .WithTopic("stress")
        .SendsCommands<SampleCommand>("sample-command")
        .ConsumesCommands("commands", ["sample-command"], 16)
        // .EmitsEvents<SampleEvent>("sample-event")
        // .SendsQueries<SampleQuery, SampleResponse>("sample-query")
        // .ConsumesEvents("events", ["sample-event"], 16)
        // .RespondsToQueries<SampleQuery, SampleResponse>("sample-query", 16)
);

services.AddHostedService<SendingService>();

var provider = services.BuildServiceProvider();
var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Program");

var hosted = provider.GetServices<IHostedService>().ToArray();
await Task.WhenAll(hosted.Select(s => s.StartAsync(default)));

await Statistics.WaitForSilence(logger, TimeSpan.FromSeconds(60));

await Task.WhenAll(hosted.Select(s => s.StopAsync(default)));
logger.LogInformation("Done");
