using MediatR;

namespace K4os.NatsTransit.Abstractions;

public interface INatsMessageBusConfigurator
{
    void Application(string name);
    void Stream(string stream, string[] subjects);

    void RequestConsumer(
        string stream, string? consumer = null,
        string[]? subjects = null);

    void CommandConsumer(
        string stream, string? consumer = null,
        string[]? subjects = null);

    void EventConsumer(
        string stream, string? consumer = null, 
        bool applicationSuffix = true,
        string[]? subjects = null);

    void QueryTarget<TRequest, TResponse>(
        string subject,
        TimeSpan? timeout = null,
        IOutboundAdapter<TRequest>? outboundAdapter = null,
        IInboundAdapter<TResponse>? inboundAdapter = null)
        where TRequest: IRequest<TResponse>;

    void RequestTarget<TRequest, TResponse>(
        string subject,
        TimeSpan? timeout = null,
        IOutboundAdapter<TRequest>? outboundAdapter = null,
        IInboundAdapter<TResponse>? inboundAdapter = null)
        where TRequest: IRequest<TResponse>;

    void CommandTarget<TCommand>(
        string subject,
        IOutboundAdapter<TCommand>? outboundAdapter = null)
        where TCommand: IRequest;

    void EventTarget<TEvent>(
        string subject,
        IOutboundAdapter<TEvent>? outboundAdapter = null)
        where TEvent: INotification;

    void QuerySource<TRequest, TResponse>(
        string subject,
        IInboundAdapter<TRequest>? inboundAdapter = null,
        IOutboundAdapter<TResponse>? outboundAdapter = null,
        int concurrency = 1)
        where TRequest: IRequest<TResponse>;

    void RequestSource<TRequest, TResponse>(
        string stream,
        string consumer,
        IInboundAdapter<TRequest>? inboundAdapter = null,
        IOutboundAdapter<TResponse>? outboundAdapter = null,
        int concurrency = 1)
        where TRequest: IRequest<TResponse>;

    void CommandSource<TCommand>(
        string stream, string consumer,
        IInboundAdapter<TCommand>? inboundAdapter = null,
        int concurrency = 1)
        where TCommand: IRequest;

    void EventSource<TEvent>(
        string stream, string consumer,
        bool applicationSuffix = true,
        IInboundAdapter<TEvent>? inboundAdapter = null,
        int concurrency = 1
    ) where TEvent: INotification;
}
