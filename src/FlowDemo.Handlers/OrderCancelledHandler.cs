using System.Net.Mail;
using FlowDemo.Entities;
using FlowDemo.Messages;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class OrderCancelledHandler: INotificationHandler<OrderCancelledEvent>
{
    protected readonly ILogger Log;
    private readonly OrdersDbContext _dbContext;

    public OrderCancelledHandler(
        ILoggerFactory loggerFactory,
        OrdersDbContext dbContext)
    {
        Log = loggerFactory.CreateLogger<OrderCreatedHandler>();
        _dbContext = dbContext;
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken token)
    {
        var orderId = notification.OrderId;
        Log.LogInformation("Order {OrderId} has been cancelled", orderId);

        var createdBy = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => o.CreatedBy)
            .FirstOrDefaultAsync(token);
        if (createdBy is null) return;
        
        var client = new SmtpClient("localhost", 1025);
        var message = new MailMessage(
            "system@brightsign.biz",
            createdBy,
            "Order Cancelled",
            $"Your order {orderId} has been cancelled");
        await client.SendMailAsync(message, token);
    }
}