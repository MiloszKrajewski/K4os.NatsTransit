using K4os.NatsTransit.Core;

namespace K4os.NatsTransit.Sources;

public interface INatsSourceConfig
{
    INatsSourceHandler CreateHandler(NatsToolbox toolbox);
}
