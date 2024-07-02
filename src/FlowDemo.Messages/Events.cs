// ReSharper disable ClassNeverInstantiated.Global

using System.Text.Json.Serialization;
using K4os.KnownTypes;
using MediatR;

namespace FlowDemo.Messages;

[KnownTypeAlias("PaymentReceivedEvent.v1")]
public record PaymentReceivedEvent: INotification
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
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

[KnownTypeAlias("OrderRejectedEvent.v1")]
public class OrderRejectedEvent: INotification
{
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; set; }
}

[KnownTypeAlias("OrderCancelledEvent.v1")]
public class OrderCancelledEvent: INotification
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
}
