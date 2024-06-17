using MediatR;

namespace K4os.NatsTransit.Abstractions;

public interface IMessageBusConfigurator
{
    // void RequestStream(string name);
    // void CommandStream(string name);
    // void EventStream(string name);

    void RequestTarget<TRequest, TResponse>(string subject)
        where TRequest: IRequest<TResponse>;

    void CommandTarget<TCommand>(string subject)
        where TCommand: IRequest;

    void EventTarget<TEvent>(string subject)
        where TEvent: INotification;

    void RequestSource<TRequest, TResponse>(string stream, string consumer)
        where TRequest: IRequest<TResponse>;

    void CommandSource<TCommand>(string stream, string subject)
        where TCommand: IRequest;
    
    void EventSource<TEvent>(string stream, string subject)
        where TEvent: INotification;
}
