using System.Text.Json.Serialization;
using K4os.KnownTypes;
using MediatR;

namespace FlowDemo.Messages;

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
