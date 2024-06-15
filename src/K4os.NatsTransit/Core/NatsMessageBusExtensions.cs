using K4os.NatsTransit.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

public static class NatsMessageBusExtensions
{
    public static IServiceCollection UseNatsMessageBus(
        this IServiceCollection services,
        Action<NatsMessageBusConfigurator> configure)
    {
        var configurator = new NatsMessageBusConfigurator();
        configure(configurator);
        services.AddSingleton<NatsMessageBus>(
            p => configurator.CreateMessageBus(
                p.GetRequiredService<ILoggerFactory>(),
                p.GetRequiredService<INatsConnection>(),
                p.GetRequiredService<INatsJSContext>(),
                p.GetRequiredService<INatsSerializerFactory>(),
                p.GetRequiredService<IExceptionSerializer>(),
                p.GetRequiredService<IMediator>().Send
            ));
        services.AddSingleton<IMessageBus>(p => p.GetRequiredService<NatsMessageBus>());
        services.AddHostedService<NatsMessageBus>(p => p.GetRequiredService<NatsMessageBus>());
        return services;
    }
}

