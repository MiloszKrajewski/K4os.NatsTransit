using System.Diagnostics;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Sources;

public class EventNatsListenerHandler<TEvent>:
    NatsSubscriber<IMessageDispatcher, TEvent, object?>.IEvents,
    INatsSourceHandler
    where TEvent: INotification
{
    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;
    private readonly string _activityName;
    private readonly string _eventType;
    private readonly int _concurrency;
    private readonly NatsSubscriber<IMessageDispatcher, TEvent, object?> _consumer;

    public record Config(
        string Subject,
        InboundPair<TEvent>? InboundPair = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new EventNatsListenerHandler<TEvent>(toolbox, this);
    }

    public EventNatsListenerHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLoggerFor(this);
        _toolbox = toolbox;
        _activityName = GetActivityName(config);
        _eventType = typeof(TEvent).GetFriendlyName();
        _concurrency = config.Concurrency.NotLessThan(1);
        var subject = config.Subject;
        var deserializer = config.InboundPair ?? toolbox.Deserializer<TEvent>();
        _consumer = NatsSubscriber.Create(toolbox, subject, this, deserializer);
    }

    private static string GetActivityName(Config config)
    {
        var eventName = typeof(TEvent).GetFriendlyName();
        var subject = config.Subject;
        return $"Listen<{eventName}>({subject})";
    }

    public IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher) => 
        _consumer.Subscribe(token, dispatcher, _concurrency);

    public Activity? OnTrace(IMessageDispatcher context, NatsHeaders? headers) =>
        _toolbox.ReceiveActivity(_activityName, headers, false);

    public Task<object?> OnHandle<TPayload>(
        CancellationToken token, IMessageDispatcher context, 
        NatsMsg<TPayload> payload, TEvent message)
    {
        _toolbox.OnEvent(message);
        return NatsToolbox.NullCompletedTask;
    }

    public Task OnSuccess<TPayload>(
        CancellationToken token, IMessageDispatcher context, 
        NatsMsg<TPayload> payload, TEvent request, object? response) =>
        Task.CompletedTask;

    public Task OnFailure<TPayload>(
        CancellationToken token, IMessageDispatcher context, 
        NatsMsg<TPayload> payload, Exception error)
    {
        Log.LogError(error, "Failed to process event {EventType} in {ActivityName}", _eventType, _activityName);
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
