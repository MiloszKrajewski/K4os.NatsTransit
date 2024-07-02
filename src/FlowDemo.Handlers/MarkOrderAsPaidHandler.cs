using FlowDemo.Entities;
using FlowDemo.Messages;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowDemo.Handlers;

public class MarkOrderAsPaidHandler: IRequestHandler<MarkOrderAsPaidCommand>
{
    private readonly OrdersDbContext _dbContext;

    public MarkOrderAsPaidHandler(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(MarkOrderAsPaidCommand request, CancellationToken token)
    {
        var orderId = request.OrderId;
        
        var order = await GetOrder(orderId, token);
        if (order is null) return;

        await MarkAsPaid(order, token);
    }

    private Task<OrderEntity?> GetOrder(Guid orderId, CancellationToken token) => 
        _dbContext.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, token);

    private async Task MarkAsPaid(OrderEntity order, CancellationToken token)
    {
        order.IsPaid = true;
        order.RowVersion++;
        await _dbContext.SaveChangesAsync(token);
    }
}
