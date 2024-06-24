using FlowDemo.Messages;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K4os.NatsTransit.Api.Handlers;

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