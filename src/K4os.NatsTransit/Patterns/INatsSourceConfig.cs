using K4os.NatsTransit.Core;

namespace K4os.NatsTransit.Patterns;

public interface INatsSourceConfig
{
    INatsSourceHandler CreateHandler(NatsToolbox toolbox);
}
