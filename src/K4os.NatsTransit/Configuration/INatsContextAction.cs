using K4os.NatsTransit.Core;

namespace K4os.NatsTransit.Configuration;

public interface INatsContextAction
{
    public Task Configure(NatsToolbox context);
}
