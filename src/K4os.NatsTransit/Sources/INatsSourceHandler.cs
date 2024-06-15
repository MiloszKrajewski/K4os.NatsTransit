namespace K4os.NatsTransit.Sources;

public interface INatsSourceHandler: IDisposable
{
    IDisposable Subscribe(CancellationToken token, MessageHandler handler);
}
