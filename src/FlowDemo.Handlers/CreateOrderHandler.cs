using FlowDemo.Entities;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using K4os.Xpovoc.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class CreateOrderHandler: IRequestHandler<CreateOrderCommand>
{
    protected readonly ILogger Log;
    private readonly IMessageBus _messageBus;
    private readonly IJobScheduler _scheduler;
    private readonly OrdersDbContext _dbContext;

    public CreateOrderHandler(
        ILoggerFactory loggerFactory, 
        IMessageBus messageBus,
        IJobScheduler scheduler,
        OrdersDbContext dbContext)
    {
        Log = loggerFactory.CreateLogger<CreateOrderHandler>();
        _messageBus = messageBus;
        _scheduler = scheduler;
        _dbContext = dbContext;
    }

    public async Task Handle(CreateOrderCommand request, CancellationToken token)
    {
        var requestId = request.RequestId;
        var orderId = Guid.NewGuid();
        
        Log.LogInformation("Creating order {OrderId} for request {RequestId}", orderId, requestId);

        var order = new OrderEntity {
            OrderId = orderId,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = request.RequestedBy ?? "anonymous@nowhere.org"
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(token);
        
        var notification = new OrderCreatedEvent {
            RequestId = requestId, 
            OrderId = orderId,
            CreatedBy = order.CreatedBy
        };
        await _messageBus.Publish(notification, token);
        
        var cancellation = new TryCancelOrderCommand { OrderId = orderId };
        await _scheduler.Schedule(DateTimeOffset.Now.AddSeconds(30), cancellation);
    }
}