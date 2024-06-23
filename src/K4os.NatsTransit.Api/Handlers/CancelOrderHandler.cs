using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using MediatR;

namespace K4os.NatsTransit.Api.Handlers;

public class CancelOrderHandler: IRequestHandler<CancelOrderCommand>
{
    protected readonly ILogger Log;
    private readonly IMessageBus _messageBus;

    public CancelOrderHandler(
        ILoggerFactory loggerFactory, 
        IMessageBus messageBus)
    {
        Log = loggerFactory.CreateLogger<CreateOrderHandler>();
        _messageBus = messageBus;
    }

    public async Task Handle(CancelOrderCommand request, CancellationToken token)
    {
        var orderId = Guid.NewGuid();
        Log.LogInformation("Cancelling order {OrderId}", orderId);
        var notification = new OrderCancelledEvent { OrderId = orderId };
        await _messageBus.Publish(notification, token);
    }
}
