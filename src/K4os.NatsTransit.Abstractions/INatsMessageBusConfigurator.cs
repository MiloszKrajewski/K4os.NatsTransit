using MediatR;

namespace K4os.NatsTransit.Abstractions;

public interface INatsMessageBusConfigurator
{
    void Application(string name);
    
    void Stream(string stream, string[] subjects);

    void Consumer(
        string stream, string consumer, string[]? subjects = null);

    void Consumer(
        string stream, string consumer, bool applicationSuffix, string[]? subjects = null);

    void QueryTarget<TRequest, TResponse>(
        string subject,
        TimeSpan? timeout = null,
        OutboundPair<TRequest>? outboundAdapter = null,
        InboundPair<TResponse>? inboundAdapter = null)
        where TRequest: IRequest<TResponse>;

    void RequestTarget<TRequest, TResponse>(
        string subject,
        TimeSpan? timeout = null,
        OutboundPair<TRequest>? outboundAdapter = null,
        InboundPair<TResponse>? inboundAdapter = null)
        where TRequest: IRequest<TResponse>;

    void CommandTarget<TCommand>(
        string subject,
        OutboundPair<TCommand>? outboundAdapter = null)
        where TCommand: IRequest;

    void EventTarget<TEvent>(
        string subject,
        OutboundPair<TEvent>? outboundAdapter = null)
        where TEvent: INotification;
    
    void EventListener<TEvent>(
        string subject, 
        InboundPair<TEvent>? inboundAdapter = null,
        int concurrency = 1)
        where TEvent: INotification;

    void QuerySource<TRequest, TResponse>(
        string subject,
        InboundPair<TRequest>? inboundAdapter = null,
        OutboundPair<TResponse>? outboundAdapter = null,
        int concurrency = 1)
        where TRequest: IRequest<TResponse>;

    void RequestSource<TRequest, TResponse>(
        string stream,
        string consumer,
        bool applicationSuffix = true,
        InboundPair<TRequest>? inboundAdapter = null,
        OutboundPair<TResponse>? outboundAdapter = null,
        int concurrency = 1)
        where TRequest: IRequest<TResponse>;

    void CommandSource<TCommand>(
        string stream, string consumer,
        bool applicationSuffix = false,
        InboundPair<TCommand>? inboundAdapter = null,
        int concurrency = 1)
        where TCommand: IRequest;

    void EventSource<TEvent>(
        string stream, string consumer,
        bool applicationSuffix = true,
        InboundPair<TEvent>? inboundAdapter = null,
        int concurrency = 1
    ) where TEvent: INotification;
}
