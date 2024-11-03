using MediatR;

namespace K4os.NatsTransit.Abstractions.MessageBus;

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
    
    private static InvalidOperationException InvalidResponseException<TQuery, TResponse>(Type actual)
        where TQuery: IRequest<TResponse> =>
        new($"Invalid response type for {typeof(TQuery).Name}, expected {typeof(TResponse).Name}, received {actual.Name}");
    
    private static InvalidOperationException NoResponseException<TEvent>()
        where TEvent: INotification =>
        new($"No event received, expected {typeof(TEvent).Name}");

    private static InvalidOperationException NoResponseException<TQuery, TResponse>()
        where TQuery: IRequest<TResponse> =>
        new($"No response received for {typeof(TQuery).Name}, expected {typeof(TResponse).Name}");
}