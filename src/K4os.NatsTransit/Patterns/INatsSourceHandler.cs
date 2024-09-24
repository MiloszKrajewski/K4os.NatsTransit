using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.MessageBus;

namespace K4os.NatsTransit.Patterns;

public interface INatsSourceHandler: IDisposable
{
    IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher);
}
