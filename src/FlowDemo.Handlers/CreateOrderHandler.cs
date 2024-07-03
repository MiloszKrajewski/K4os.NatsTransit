using FlowDemo.Entities;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class CreateOrderHandler: IRequestHandler<CreateOrderCommand>
{
    protected readonly ILogger Log;
    private readonly IMessageBus _messageBus;
    private readonly OrdersDbContext _dbContext;

    public CreateOrderHandler(
        ILoggerFactory loggerFactory,
        IMessageBus messageBus,
        OrdersDbContext dbContext)
    {
        Log = loggerFactory.CreateLogger<CreateOrderHandler>();
        _messageBus = messageBus;
        _dbContext = dbContext;
    }

    public async Task Handle(CreateOrderCommand request, CancellationToken token)
    {
        var requestId = request.RequestId;
        var orderId = Guid.NewGuid();

        if (string.IsNullOrWhiteSpace(request.RequestedBy))
        {
            await SendOrderRejectedEvent(requestId, token);
        }
        else
        {
            var order = await CreateOrder(orderId, request, token);
            await SendOrderCreatedEvent(request, order, token);
        }
    }

    private async Task<OrderEntity> CreateOrder(
        Guid orderId, CreateOrderCommand request, CancellationToken token)
    {
        Log.LogInformation(
            "Creating order {OrderId} for request {RequestId}",
            orderId, request.RequestId);

        var order = new OrderEntity {
            OrderId = orderId,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = request.RequestedBy ?? "anonymous@nowhere.org"
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(token);
        return order;
    }

    private async Task SendOrderCreatedEvent(
        CreateOrderCommand request, OrderEntity order, CancellationToken token)
    {
        var paymentWindow = request.PaymentWindow ?? 30;
        var notification = new OrderCreatedEvent {
            RequestId = request.RequestId,
            OrderId = order.OrderId,
            CreatedBy = order.CreatedBy,
            PaymentWindowEndsOn = DateTime.UtcNow.AddSeconds(paymentWindow)
        };
        await _messageBus.Publish(notification, token);
    }

    private async Task SendOrderRejectedEvent(Guid requestId, CancellationToken token)
    {
        var notification = new OrderRejectedEvent {
            RequestId = requestId,
        };
        await _messageBus.Publish(notification, token);
    }
}
