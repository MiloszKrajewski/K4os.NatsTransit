using K4os.NatsTransit.Core;

namespace K4os.NatsTransit.Targets;

public interface INatsTargetConfig
{
    INatsTargetHandler CreateHandler(NatsToolbox toolbox);
}
