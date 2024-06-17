using K4os.NatsTransit.Abstractions;
using MediatR;

namespace K4os.NatsTransit.Api.Handlers;

public class CreateOrderHandler: IRequestHandler<CreateOrderCommand>
{
    protected readonly ILogger Log;
    private readonly IMessageBus _messageBus;

    public CreateOrderHandler(ILoggerFactory loggerFactory, IMessageBus messageBus)
    {
        Log = loggerFactory.CreateLogger<CreateOrderHandler>();
        _messageBus = messageBus;
    }

    public async Task Handle(CreateOrderCommand request, CancellationToken token)
    {
        var requestId = request.RequestId;
        var orderId = Guid.NewGuid();
        Log.LogInformation("Creating order {OrderId} for request {RequestId}", orderId, requestId);
        var notification = new OrderCreatedEvent { RequestId = requestId, OrderId = orderId };
        await _messageBus.Publish(notification, token);
    }
}