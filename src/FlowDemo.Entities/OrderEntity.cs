using System.ComponentModel.DataAnnotations;

namespace FlowDemo.Entities;

public class OrderEntity
{
    public Guid OrderId { get; set; }
    [MaxLength(1024)]
    public required string CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public bool IsPaid { get; set; }
    public bool IsCancelled { get; set; }
    public long RowVersion { get; set; }
}