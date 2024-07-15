using System.Buffers;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Sources;

public class EventNatsListenerHandler<TEvent>: INatsSourceHandler 
    where TEvent: INotification
{
    protected readonly ILogger Log;
    
    public static INatsDeserialize<IMemoryOwner<byte>> BinaryDeserializer =>
        NatsRawSerializer<IMemoryOwner<byte>>.Default;

    public static IInboundAdapter<TEvent, TEvent> NullInboundAdapter => 
        NullInboundAdapter<TEvent>.Default;
    
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly INatsDeserialize<TEvent> _requestDeserializer;
    private readonly IInboundAdapter<TEvent>? _inboundAdapter;
    private readonly int _concurrency;
    private readonly DisposableBag _disposables;
    private readonly string _activityName;

    public record Config(
        string Subject,
        IInboundAdapter<TEvent>? InboundAdapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new EventNatsListenerHandler<TEvent>(toolbox, this);
    }

    public EventNatsListenerHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _subject = config.Subject;
        _requestDeserializer = toolbox.Deserializer<TEvent>();
        _inboundAdapter = config.InboundAdapter;
        _concurrency = config.Concurrency.NotLessThan(1);
        _disposables = new DisposableBag();
        var eventName = typeof(TEvent).Name;
        _activityName = $"Listen<{eventName}>({_subject})";
    }

    public IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var agents = Enumerable
            .Range(0, _concurrency)
            .Select(_ => Agent.Launch(c => Consume(c, dispatcher), Log, cts.Token));
        _disposables.AddMany(agents);
        return Disposable.Create(cts.Cancel);
    }

    private Task Consume(IAgentContext context, IMessageDispatcher mediator) =>
        _inboundAdapter is null
            ? Consume(context, mediator, _requestDeserializer, NullInboundAdapter)
            : Consume(context, mediator, BinaryDeserializer, _inboundAdapter);

    private async Task Consume<TPayload>(
        IAgentContext context, 
        IMessageDispatcher mediator,
        INatsDeserialize<TPayload> deserializer,
        IInboundAdapter<TPayload, TEvent> adapter)
    {
        var token = context.Token;
        var consumer = _toolbox.SubscribeMany(token, _subject, deserializer);
        await foreach (var message in consumer)
            await ConsumeOne(token, message, adapter, mediator);
    }

    protected Task ConsumeOne<TPayload>(
        CancellationToken token, 
        NatsMsg<TPayload> message, 
        IInboundAdapter<TPayload, TEvent> adapter, 
        IMessageDispatcher mediator)
    {
        using var _ = _toolbox.ReceiveActivity(_activityName, message.Headers, false);
        try
        {
            var request = Unpack(message, adapter);
            _toolbox.OnEvent(request);
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to process message");
        }
        return Task.CompletedTask;
    }

    private TEvent Unpack<TPayload>(
        NatsMsg<TPayload> message, IInboundAdapter<TPayload, TEvent> adapter) => 
        _toolbox.Unpack(message, adapter);

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
