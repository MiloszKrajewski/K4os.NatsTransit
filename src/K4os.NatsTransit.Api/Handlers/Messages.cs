// ReSharper disable ClassNeverInstantiated.Global

using K4os.KnownTypes;
using MediatR;

namespace K4os.NatsTransit.Api.Handlers;

[KnownTypeAlias("CreateOrderCommand.v1")]
public class CreateOrderCommand: IRequest
{
    public Guid RequestId { get; set; }
}

[KnownTypeAlias("OrderCreatedEvent.v1")]
public class OrderCreatedEvent: INotification
{
    public Guid RequestId { get; set; }
    public Guid OrderId { get; set; }
}

[KnownTypeAlias("GetOrderQuery.v1")]
public class GetOrderQuery: IRequest<OrderResponse>
{
    public Guid OrderId { get; set; }
}

[KnownTypeAlias("OrderResponse.v1")]
public class OrderResponse
{
    public Guid OrderId { get; set; }
}