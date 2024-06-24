namespace K4os.NatsTransit.Core;

public interface INatsContextAction
{
    public Task Configure(NatsToolbox context);
}
