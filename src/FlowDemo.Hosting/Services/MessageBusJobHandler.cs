using K4os.NatsTransit.Abstractions;
using K4os.Xpovoc.Abstractions;
using MediatR;

namespace FlowDemo.Hosting.Services;

public class MessageBusJobHandler: IJobHandler
{
    private readonly IMessageBus _messageBus;

    public MessageBusJobHandler(IMessageBus messageBus)
    {
        _messageBus = messageBus;        
    }
    
    public Task Handle(CancellationToken token, object payload) =>
        payload switch {
            INotification notification => _messageBus.Publish(notification, token),
            IRequest request => _messageBus.Send(request, token),
            _ => throw new NotSupportedException(
                $"Payload of type {payload.GetType().Name} is not supported")
        };
}