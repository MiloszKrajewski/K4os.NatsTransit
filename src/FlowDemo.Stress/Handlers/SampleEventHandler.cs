using FlowDemo.Stress.Messages;
using MediatR;

namespace FlowDemo.Stress.Handlers;

public class SampleEventHandler: INotificationHandler<SampleEvent>
{
    public Task Handle(SampleEvent notification, CancellationToken cancellationToken)
    {
        Statistics.OnMessageReceived();
        return Task.CompletedTask;
    }
}
