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
    
    [JsonPropertyName("payment_window")]
    public int? PaymentWindow { get; set; }
}

[KnownTypeAlias("MarkOrderAsPaidCommand.v1")]
public class MarkOrderAsPaidCommand: IRequest
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
}

[KnownTypeAlias("TryCancelOrderCommand.v1")]
public class TryCancelOrderCommand: IRequest
{
    [JsonPropertyName("order_id")]
    public Guid OrderId { get; set; }
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
