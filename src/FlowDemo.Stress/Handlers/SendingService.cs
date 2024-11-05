using System.Diagnostics;
using FlowDemo.Stress.Messages;
using K4os.NatsTransit.Abstractions.MessageBus;
using Microsoft.Extensions.Hosting;

namespace FlowDemo.Stress.Handlers;

public class SendingService: BackgroundService
{
    private static readonly TimeSpan Burst = TimeSpan.FromSeconds(5);
    private const double SendRateLimit = 150_000;
    private const int Threads = 16;

    private readonly IMessageBus _messageBus;
    private long _started;
    private long _sent;

    public SendingService(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(Burst);
        var token = cts.Token;

        _started = Stopwatch.GetTimestamp();
        _sent = 0;
        var tasks = Enumerable
            .Range(0, Threads)
            .Select(_ => Task.Run(() => SendMessages(token), token))
            .ToArray();

        return Task.WhenAll(tasks);
    }

    private async Task? SendMessages(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var message = new SampleCommand { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow };
                await _messageBus.Send(message, token);
                Statistics.OnMessageSent();
                Interlocked.Increment(ref _sent);
                await LimitSendRate(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // ignore
        }
    }

    private async Task LimitSendRate(CancellationToken token)
    {
        while (true)
        {
            var elapsed = Stopwatch.GetElapsedTime(_started).TotalSeconds;
            if (_sent / elapsed <= SendRateLimit) break;

            await Task.Delay(1, token);
        }
    }
}
