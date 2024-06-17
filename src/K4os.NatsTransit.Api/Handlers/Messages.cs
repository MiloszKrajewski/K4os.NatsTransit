// ReSharper disable ClassNeverInstantiated.Global

using MediatR;

namespace K4os.NatsTransit.Api.Handlers;

public class CreateOrderCommand: IRequest
{
    public Guid RequestId { get; set; }
}

public class OrderCreatedEvent: INotification
{
    public Guid RequestId { get; set; }
    public Guid OrderId { get; set; }
}

public class GetOrderQuery: IRequest<OrderResponse>
{
    public Guid OrderId { get; set; }
}

public class OrderResponse
{
    public Guid OrderId { get; set; }
}