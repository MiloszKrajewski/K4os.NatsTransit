using FlowDemo.Messages;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K4os.NatsTransit.Api.Handlers;

public class OrderCancelledHandler: INotificationHandler<OrderCancelledEvent>
{
    protected readonly ILogger Log;

    public OrderCancelledHandler(ILoggerFactory loggerFactory)
    {
        Log = loggerFactory.CreateLogger<OrderCreatedHandler>();
    }

    public Task Handle(OrderCancelledEvent notification, CancellationToken token)
    {
        var orderId = notification.OrderId;
        Log.LogInformation("Order {OrderId} has been cancelled", orderId);
        return Task.CompletedTask;
    }
}