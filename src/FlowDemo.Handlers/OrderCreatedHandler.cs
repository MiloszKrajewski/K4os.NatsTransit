using FlowDemo.Messages;
using K4os.Xpovoc.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Handlers;

public class OrderCreatedHandler: INotificationHandler<OrderCreatedEvent>
{
    protected readonly ILogger Log;
    private readonly IJobScheduler _scheduler;

    public OrderCreatedHandler(ILoggerFactory loggerFactory, IJobScheduler scheduler)
    {
        Log = loggerFactory.CreateLogger<OrderCreatedHandler>();
        _scheduler = scheduler;
    }

    public async Task Handle(OrderCreatedEvent notification, CancellationToken token)
    {
        await ScheduleCancellation(notification);
    }

    private async Task ScheduleCancellation(OrderCreatedEvent notification)
    {
        var cancellation = new TryCancelOrderCommand { OrderId = notification.OrderId };
        var cancellationTime =
            notification.PaymentWindowEndsOn ?? 
            DateTimeOffset.Now.AddSeconds(30);
        Log.LogInformation(
            "Scheduling cancellation for order {OrderId} at {CancellationTime}",
            notification.OrderId, cancellationTime);
        await _scheduler.Schedule(cancellationTime, cancellation);
    }
}
