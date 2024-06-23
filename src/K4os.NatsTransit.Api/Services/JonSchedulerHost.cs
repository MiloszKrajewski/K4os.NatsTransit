using K4os.Xpovoc.Abstractions;

namespace K4os.NatsTransit.Api.Services;

public class JonSchedulerHost: IHostedService
{
    private readonly IServiceProvider _provider;
    private IJobScheduler? _scheduler;

    public JonSchedulerHost(IServiceProvider provider)
    {
        _provider = provider;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler = _provider.GetRequiredService<IJobScheduler>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _scheduler?.Dispose();
        _scheduler = null;
        return Task.CompletedTask;
    }
}
