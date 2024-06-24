// ReSharper disable ClassNeverInstantiated.Global

using System.Text.Json.Serialization;
using K4os.KnownTypes;
using MediatR;

namespace FlowDemo.Messages;

[KnownTypeAlias("CreateOrderCommand.v1")]
public class CreateOrderCommand: IRequest
{
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; set; }
}

[KnownTypeAlias("OrderCreatedEvent.v1")]
public class OrderCreatedEvent: INotification
{
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; set; }
    
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
}

[KnownTypeAlias("CancelOrderCommand.v1")]
public class CancelOrderCommand: IRequest
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
}

[KnownTypeAlias("OrderCancelledEvent.v1")]
public class OrderCancelledEvent: INotification
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
}

[KnownTypeAlias("GetOrderQuery.v1")]
public class GetOrderQuery: IRequest<OrderResponse>
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
}

[KnownTypeAlias("OrderResponse.v1")]
public class OrderResponse
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
}