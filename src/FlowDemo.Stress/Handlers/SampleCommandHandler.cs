using FlowDemo.Stress.Messages;
using MediatR;

namespace FlowDemo.Stress.Handlers;

public class SampleCommandHandler: IRequestHandler<SampleCommand>
{
    public Task Handle(SampleCommand request, CancellationToken cancellationToken)
    {
        Statistics.OnMessageReceived();
        return Task.CompletedTask;
    }
}