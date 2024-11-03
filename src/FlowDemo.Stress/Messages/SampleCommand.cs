using System.Runtime.Serialization;
using MediatR;

namespace FlowDemo.Stress.Messages;

[DataContract]
public class SampleCommand: IRequest
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
}
