using K4os.NatsTransit.Abstractions;

namespace K4os.NatsTransit.Sources;

public interface INatsSourceHandler: IDisposable
{
    IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher);
}
