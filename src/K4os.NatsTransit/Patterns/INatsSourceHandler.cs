using K4os.NatsTransit.Abstractions;

namespace K4os.NatsTransit.Patterns;

public interface INatsSourceHandler: IDisposable
{
    IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher);
}
