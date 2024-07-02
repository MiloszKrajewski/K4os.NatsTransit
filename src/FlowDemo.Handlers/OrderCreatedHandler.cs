using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class OrderCreatedHandler: INotificationHandler<OrderCreatedEvent>
{
    protected readonly ILogger Log;

    public OrderCreatedHandler(ILoggerFactory loggerFactory)
    {
        Log = loggerFactory.CreateLogger<OrderCreatedHandler>();
    }

    public Task Handle(OrderCreatedEvent notification, CancellationToken token)
    {
        var requestId = notification.RequestId;
        var orderId = notification.OrderId;
        Log.LogInformation("Order {OrderId} created for request {RequestId}", orderId, requestId);
        return Task.CompletedTask;
    }
}