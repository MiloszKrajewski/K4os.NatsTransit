using System.Buffers;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Sources;

public class EventNatsListenerHandler<TRequest>: INatsSourceHandler 
    where TRequest: INotification
{
    protected readonly ILogger Log;
    
    public static INatsDeserialize<IMemoryOwner<byte>> BinaryDeserializer =>
        NatsRawSerializer<IMemoryOwner<byte>>.Default;

    public static IInboundAdapter<TRequest, TRequest> NullInboundAdapter => 
        NullInboundAdapter<TRequest>.Default;
    
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly INatsDeserialize<TRequest> _requestDeserializer;
    private readonly IInboundAdapter<TRequest>? _inboundAdapter;
    private readonly int _concurrency;
    private readonly DisposableBag _disposables;

    public record Config(
        string Subject,
        IInboundAdapter<TRequest>? InboundAdapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new EventNatsListenerHandler<TRequest>(toolbox, this);
    }

    public EventNatsListenerHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _subject = config.Subject;
        _requestDeserializer = toolbox.Deserializer<TRequest>();
        _inboundAdapter = config.InboundAdapter;
        _concurrency = config.Concurrency.NotLessThan(1);
        _disposables = new DisposableBag();
    }

    public IDisposable Subscribe(CancellationToken token, IMessageDispatcher mediator)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var agents = Enumerable
            .Range(0, _concurrency)
            .Select(_ => Agent.Launch(c => Consume(c, mediator), Log, cts.Token));
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
        IInboundAdapter<TPayload, TRequest> adapter)
    {
        var token = context.Token;
        var consumer = _toolbox.SubscribeMany(token, _subject, deserializer);
        await foreach (var message in consumer)
            await ConsumeOne(token, message, adapter, mediator);
    }

    protected Task ConsumeOne<TPayload>(
        CancellationToken token, 
        NatsMsg<TPayload> message, 
        IInboundAdapter<TPayload, TRequest> adapter, 
        IMessageDispatcher mediator)
    {
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

    private TRequest Unpack<TPayload>(
        NatsMsg<TPayload> message, IInboundAdapter<TPayload, TRequest> adapter) => 
        _toolbox.Unpack(message, adapter);

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
