using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

public static class NatsMessageBusConfiguratorExtensions
{
    public static IServiceCollection UseNatsMessageBus(
        this IServiceCollection services,
        Action<IFluentNats> configure)
    {
        services.AddSingleton<NatsMessageBus>(
            p => {
                var configurator = new NatsMessageBusConfigurator();
                var fluent = new FluentNats(configurator);
                configure(fluent);
                return configurator.CreateMessageBus(
                    p.GetRequiredService<ILoggerFactory>(),
                    p.GetRequiredService<INatsConnection>(),
                    p.GetRequiredService<INatsJSContext>(),
                    p.GetRequiredService<INatsSerializers>(),
                    p.GetRequiredService<IMessageDispatcher>(),
                    p.GetService<INatsMessageTracer>());
            });
        services.AddSingleton<IMessageBus>(p => p.GetRequiredService<NatsMessageBus>());
        services.AddHostedService<NatsMessageBus>(p => p.GetRequiredService<NatsMessageBus>());
        return services;
    }
}
