using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using MediatR;

namespace FlowDemo.Handlers;

public class PaymentReceivedHandler: INotificationHandler<PaymentReceivedEvent>
{
    private readonly IMessageBus _messageBus;

    public PaymentReceivedHandler(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    public Task Handle(PaymentReceivedEvent notification, CancellationToken token) =>
        _messageBus.Send(new MarkOrderAsPaidCommand { OrderId = notification.OrderId }, token);
}