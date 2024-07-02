using FlowDemo.Entities;
using FlowDemo.Messages;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class GetOrderHandler: IRequestHandler<GetOrderQuery, OrderResponse>
{
    protected readonly ILogger Log;
    private readonly OrdersDbContext _dbContext;

    public GetOrderHandler(
        ILoggerFactory loggerFactory,
        OrdersDbContext dbContext)
    {
        Log = loggerFactory.CreateLogger<GetOrderHandler>();
        _dbContext = dbContext;
    }

    public async Task<OrderResponse> Handle(GetOrderQuery request, CancellationToken token)
    {
        var orderId = request.OrderId;

        Log.LogInformation("Getting order {OrderId} details", orderId);

        var order = await GetOrder(token, orderId);

        return order is null
            ? new OrderResponse { Found = false }
            : new OrderResponse {
                Found = true,
                OrderId = order.OrderId,
                CreatedBy = order.CreatedBy,
                IsPaid = order.IsPaid,
                IsCancelled = order.IsCancelled
            };
    }

    private async Task<OrderEntity?> GetOrder(CancellationToken token, Guid orderId) =>
        await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, token);
}
