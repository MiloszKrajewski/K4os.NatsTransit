using MediatR;

namespace K4os.NatsTransit.Abstractions;

public interface IMessageBus
{
    Task Send<TCommand>(
        TCommand command, CancellationToken token = default)
        where TCommand: IRequest;

    Task Publish<TEvent>(
        TEvent @event, CancellationToken token = default)
        where TEvent: INotification;

    Task<TResponse> Query<TQuery, TResponse>(
        TQuery query, CancellationToken token = default)
        where TQuery: IRequest<TResponse>;
    
    Task<TResponse> Request<TRequest, TResponse>(
        TRequest request, CancellationToken token = default)
        where TRequest: IRequest<TResponse>;
}
