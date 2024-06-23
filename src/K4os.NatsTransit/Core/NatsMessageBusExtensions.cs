using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

public static class NatsMessageBusExtensions
{
    public static Task Send<TCommand>(
        this IMessageBus bus, TCommand command, CancellationToken token = default)
        where TCommand: IRequest =>
        bus.Dispatch(command.ThrowIfNull(), token);

    public static Task Publish<TEvent>(
        this IMessageBus bus, TEvent @event, CancellationToken token = default)
        where TEvent: INotification =>
        bus.Dispatch(@event.ThrowIfNull(), token);

    public static async Task<TResponse> Query<TQuery, TResponse>(
        this IMessageBus bus,
        TQuery query, CancellationToken token = default)
        where TQuery: IRequest<TResponse> =>
        await bus.Dispatch(query, token) switch {
            TResponse r => r,
            null => throw NoResponseException<TQuery, TResponse>(),
            var r => throw InvalidResponseException<TQuery, TResponse>(r.GetType())
        };

    public static async Task<TResponse> Request<TRequest, TResponse>(
        this IMessageBus bus,
        TRequest query, CancellationToken token = default)
        where TRequest: IRequest<TResponse> =>
        await bus.Dispatch(query, token) switch {
            TResponse r => r,
            null => throw NoResponseException<TRequest, TResponse>(),
            var r => throw InvalidResponseException<TRequest, TResponse>(r.GetType())
        };

    public static async Task<TEvent> Await<TEvent>(
        this IMessageBus bus,
        Func<TEvent, bool> predicate,
        TimeSpan? timeout = null,
        CancellationToken token = default)
        where TEvent: class, INotification
    {
        var response = await bus.Await(o => o is TEvent e && predicate(e), timeout, token);
        return response as TEvent ?? throw NoResponseException<TEvent>();
    }

    private static Exception InvalidResponseException<TQuery, TResponse>(Type actual)
        where TQuery: IRequest<TResponse> =>
        new InvalidOperationException(
            $"Invalid response type for {typeof(TQuery).Name}, expected {typeof(TResponse).Name}, received {actual.Name}");
    
    private static Exception NoResponseException<TEvent>()
        where TEvent: INotification =>
        new InvalidOperationException(
            $"No event received, expected {typeof(TEvent).Name}");

    private static Exception NoResponseException<TQuery, TResponse>()
        where TQuery: IRequest<TResponse> =>
        new InvalidOperationException(
            $"No response received for {typeof(TQuery).Name}, expected {typeof(TResponse).Name}");

    public static IServiceCollection UseNatsMessageBus(
        this IServiceCollection services,
        Action<NatsMessageBusConfigurator> configure)
    {
        services.AddSingleton<NatsMessageBus>(
            p => {
                var configurator = new NatsMessageBusConfigurator();
                configure(configurator);
                return configurator.CreateMessageBus(
                    p.GetRequiredService<ILoggerFactory>(),
                    p.GetRequiredService<INatsConnection>(),
                    p.GetRequiredService<INatsJSContext>(),
                    p.GetRequiredService<INatsSerializerFactory>(),
                    p.GetRequiredService<IExceptionSerializer>(),
                    p.GetRequiredService<IMessageDispatcher>());
            });
        services.AddSingleton<IMessageBus>(p => p.GetRequiredService<NatsMessageBus>());
        services.AddHostedService<NatsMessageBus>(p => p.GetRequiredService<NatsMessageBus>());
        return services;
    }
}
