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

    public CreateOrderHandler(
        ILoggerFactory loggerFactory, 
        IMessageBus messageBus,
        IJobScheduler scheduler)
    {
        Log = loggerFactory.CreateLogger<CreateOrderHandler>();
        _messageBus = messageBus;
        _scheduler = scheduler;
    }

    public async Task Handle(CreateOrderCommand request, CancellationToken token)
    {
        var requestId = request.RequestId;
        var orderId = Guid.NewGuid();
        Log.LogInformation("Creating order {OrderId} for request {RequestId}", orderId, requestId);
        var notification = new OrderCreatedEvent { RequestId = requestId, OrderId = orderId };
        await _messageBus.Publish(notification, token);
        
        var cancellation = new CancelOrderCommand { OrderId = orderId };
        await _scheduler.Schedule(DateTimeOffset.Now.AddSeconds(10), cancellation);
    }
}