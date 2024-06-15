using K4os.NatsTransit.Core;

namespace K4os.NatsTransit.Sources;

public delegate Task<object?> MessageHandler(object message, CancellationToken token);

public interface INatsSourceConfig
{
    INatsSourceHandler CreateHandler(NatsToolbox toolbox);
}
