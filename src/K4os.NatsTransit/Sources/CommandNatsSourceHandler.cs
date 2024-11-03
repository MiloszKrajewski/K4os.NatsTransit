using System.Diagnostics;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Sources;

public class CommandNatsSourceHandler<TCommand>:
    NatsConsumer<IMessageDispatcher, TCommand, object?>.IEvents,
    INatsSourceHandler 
    where TCommand: IRequest
{
    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;
    private readonly string _activityName;
    private readonly string _commandType;
    private readonly int _concurrency;
    private readonly NatsConsumer<IMessageDispatcher, TCommand, object?> _consumer;

    public record Config(
        string Stream, string Consumer,
        InboundAdapter<TCommand>? InboundAdapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new CommandNatsSourceHandler<TCommand>(toolbox, this);
    }

    public CommandNatsSourceHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLoggerFor(this);
        _toolbox = toolbox;
        _activityName = GetActivityName(config);
        _commandType = typeof(TCommand).GetFriendlyName();
        _concurrency = config.Concurrency.NotLessThan(1);
        var streamName = config.Stream;
        var consumerName = config.Consumer;
        var deserializer = config.InboundAdapter ?? toolbox.GetInboundAdapter<TCommand>();
        _consumer = NatsConsumer.Create(toolbox, streamName, consumerName, this, deserializer);
    }

    private static string GetActivityName(Config config)
    {
        var commandType = typeof(TCommand).GetFriendlyName();
        var streamName = config.Stream;
        var consumerName = config.Consumer;
        return $"Consume<{commandType}>({streamName}/{consumerName})";
    }

    public IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher) => 
        _consumer.Subscribe(token, dispatcher, _concurrency);

    public Activity? OnTrace(IMessageDispatcher context, NatsHeaders? headers) => 
        _toolbox.Tracing.ReceivedScope(_activityName, headers, false);

    public Task<object?> OnHandle<TPayload>(
        CancellationToken token, IMessageDispatcher dispatcher, 
        NatsJSMsg<TPayload> payload, TCommand message) =>
        _toolbox.Metrics.HandleScope(
            payload.Subject, 
            () => dispatcher.ForkDispatch<TCommand, object?>(message, token));

    public Task OnSuccess<TPayload>(
        CancellationToken token, IMessageDispatcher dispatcher, 
        NatsJSMsg<TPayload> payload, TCommand request, object? response) => 
        Task.CompletedTask;

    public Task OnFailure<TPayload>(
        CancellationToken token, IMessageDispatcher dispatcher, 
        NatsJSMsg<TPayload> payload, Exception error)
    {
        Log.LogError(error, "Failed to process command {CommandType} in {ActivityName}", _commandType, _activityName);
        return Task.CompletedTask;
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        _consumer.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
