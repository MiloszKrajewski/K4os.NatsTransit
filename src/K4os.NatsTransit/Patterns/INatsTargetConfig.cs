using K4os.NatsTransit.Core;

namespace K4os.NatsTransit.Patterns;

public interface INatsTargetConfig
{
    INatsTargetHandler CreateHandler(NatsToolbox toolbox);
}
