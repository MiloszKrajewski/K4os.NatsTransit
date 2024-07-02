using FlowDemo.Entities;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class TryCancelOrderHandler: IRequestHandler<TryCancelOrderCommand>
{
    protected readonly ILogger Log;
    private readonly IMessageBus _messageBus;
    private readonly OrdersDbContext _dbContext;

    public TryCancelOrderHandler(
        ILoggerFactory loggerFactory, 
        IMessageBus messageBus,
        OrdersDbContext dbContext)
    {
        Log = loggerFactory.CreateLogger<CreateOrderHandler>();
        _messageBus = messageBus;
        _dbContext = dbContext;
    }

    public async Task Handle(TryCancelOrderCommand request, CancellationToken token)
    {
        var orderId = request.OrderId;
        Log.LogInformation("Cancelling order {OrderId}", orderId);

        var order = await GetOrder(token, orderId);
        if (order == null) return;

        await MarkOrderAsCancelled(order, token);
        await SendOrderCancelledEvent(orderId, token);
    }

    private Task<OrderEntity?> GetOrder(CancellationToken token, Guid orderId) =>
        _dbContext.Orders
            .Where(o => o.OrderId == orderId && !o.IsPaid && !o.IsCancelled)
            .FirstOrDefaultAsync(token);
    
    private async Task SendOrderCancelledEvent(Guid orderId, CancellationToken token)
    {
        var notification = new OrderCancelledEvent { OrderId = orderId };
        await _messageBus.Publish(notification, token);
    }
    
    private async Task MarkOrderAsCancelled(OrderEntity order, CancellationToken token)
    {
        order.IsCancelled = true;
        order.RowVersion++;
        await _dbContext.SaveChangesAsync(token);
    }
}
