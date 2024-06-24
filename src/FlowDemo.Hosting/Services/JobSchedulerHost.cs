using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowDemo.Hosting.Services;

public class JobSchedulerHost: IHostedService
{
    private readonly IServiceProvider _provider;
    private IJobScheduler? _scheduler;

    public JobSchedulerHost(IServiceProvider provider)
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
