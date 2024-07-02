// ReSharper disable ClassNeverInstantiated.Global

using System.Text.Json.Serialization;
using K4os.KnownTypes;
using MediatR;

namespace FlowDemo.Messages;

[KnownTypeAlias("CreateOrderCommand.v1")]
public record CreateOrderCommand: IRequest
{
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; set; }

    [JsonPropertyName("requested_by")]
    public string? RequestedBy { get; set; }
}

[KnownTypeAlias("OrderCreatedEvent.v1")]
public class OrderCreatedEvent: INotification
{
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; set; }
    
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }
}

[KnownTypeAlias("TryCancelOrderCommand.v1")]
public class TryCancelOrderCommand: IRequest
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
    [JsonPropertyName("found")]
    public bool Found { get; set; }
    
    [JsonPropertyName("order_id")]
    public Guid? OrderId { get; set; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("is_paid")]
    public bool IsPaid { get; set; }
    
    [JsonPropertyName("is_cancelled")]
    public bool IsCancelled { get; set; }
}

[KnownTypeAlias("SendNotification.v1")]
public class SendNotificationCommand: IRequest
{
    [JsonPropertyName("email")]
    public string? Recipient { get; set; }
    
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }
    
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}
