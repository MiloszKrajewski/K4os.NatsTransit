using K4os.NatsTransit.Core;

namespace K4os.NatsTransit.Abstractions;

public interface INatsContextAction
{
    public Task Configure(NatsToolbox context);
}
