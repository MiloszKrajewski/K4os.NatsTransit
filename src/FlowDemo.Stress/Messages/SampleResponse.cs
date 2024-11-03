using System.Runtime.Serialization;

namespace FlowDemo.Stress.Messages;

[DataContract]
public class SampleResponse
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
}