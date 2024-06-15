using K4os.NatsTransit.Abstractions;

namespace K4os.NatsTransit.Core;

public class NatsContextAction: INatsContextAction
{
    private readonly Func<NatsToolbox, Task> _action;

    public NatsContextAction(Func<NatsToolbox, Task> action) =>
        _action = action;

    public Task Configure(NatsToolbox context) =>
        _action(context);
}
