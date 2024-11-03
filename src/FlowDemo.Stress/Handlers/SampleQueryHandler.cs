using FlowDemo.Stress.Messages;
using MediatR;

namespace FlowDemo.Stress.Handlers;

public class SampleQueryHandler: IRequestHandler<SampleQuery, SampleResponse>
{
    public Task<SampleResponse> Handle(SampleQuery request, CancellationToken cancellationToken)
    {
        Statistics.OnMessageReceived();
        var response = new SampleResponse { Id = request.Id, Timestamp = request.Timestamp };
        return Task.FromResult(response);
    }
}

