﻿using FlowDemo.Entities;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions.MessageBus;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class OrderCancelledHandler: INotificationHandler<OrderCancelledEvent>
{
    protected readonly ILogger Log;
    private readonly OrdersDbContext _dbContext;
    private readonly IMessageBus _messageBus;

    public OrderCancelledHandler(
        ILoggerFactory loggerFactory,
        IMessageBus messageBus,
        OrdersDbContext dbContext)
    {
        Log = loggerFactory.CreateLogger<OrderCreatedHandler>();
        _messageBus = messageBus;
        _dbContext = dbContext;
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken token)
    {
        var orderId = notification.OrderId;
        Log.LogInformation("Order {OrderId} has been cancelled", orderId);

        var createdBy = await GetCreatedBy(orderId, token);
        if (createdBy is null) return;

        await SendCancellationEmail(orderId, createdBy, token);
    }

    private async Task<string?> GetCreatedBy(Guid orderId, CancellationToken token)
    {
        var createdBy = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => o.CreatedBy)
            .FirstOrDefaultAsync(token);
        return createdBy;
    }
    
    private async Task SendCancellationEmail(
        Guid orderId, string createdBy, CancellationToken token)
    {
        var command = new SendNotificationCommand {
            Recipient = createdBy,
            Subject = "Order Cancelled",
            Body = $"Your order {orderId} has been cancelled"
        };
        await _messageBus.Send(command, token);
    }
}