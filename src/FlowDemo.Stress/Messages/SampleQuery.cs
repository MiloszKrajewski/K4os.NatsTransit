using System.Runtime.Serialization;
using MediatR;

namespace FlowDemo.Stress.Messages;

[DataContract]
public class SampleQuery: IRequest<SampleResponse>
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
}
