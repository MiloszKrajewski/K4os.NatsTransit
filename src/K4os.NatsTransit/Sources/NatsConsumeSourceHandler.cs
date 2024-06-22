using System.Buffers;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Sources;

public abstract class NatsConsumeSourceHandler<TMessage>:
    INatsSourceHandler
    where TMessage: notnull
{
    public static INatsDeserialize<IMemoryOwner<byte>> BinaryDeserializer =>
        NatsRawSerializer<IMemoryOwner<byte>>.Default;

    public static IInboundAdapter<TMessage, TMessage> NullInboundAdapter => 
        NullInboundAdapter<TMessage>.Default;

    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;
    private readonly string _stream;
    private readonly string _consumer;
    private readonly INatsDeserialize<TMessage> _deserializer;
    private readonly IInboundAdapter<TMessage>? _adapter;
    private readonly int _concurrency;
    private readonly DisposableBag _disposables;

    protected NatsConsumeSourceHandler(
        NatsToolbox toolbox, 
        string stream, string consumer,
        IInboundAdapter<TMessage>? adapter = null,
        int concurrency = 1)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _stream = stream;
        _consumer = consumer;
        _deserializer = toolbox.Deserializer<TMessage>();
        _adapter = adapter;
        _concurrency = concurrency.NotLessThan(1);
        _disposables = new DisposableBag();
    }
    
    protected NatsToolbox Toolbox => _toolbox;

    public IDisposable Subscribe(CancellationToken token, IMediatorAdapter mediator)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var agents = Enumerable
            .Range(0, _concurrency)
            .Select(_ => Agent.Launch(c => Consume(c, mediator), Log, cts.Token));
        _disposables.AddMany(agents);
        return Disposable.Create(cts.Cancel);
    }

    private Task Consume(IAgentContext context, IMediatorAdapter mediator)
    {
        var token = context.Token;
        return _adapter is null
            ? Consume(token, mediator, _deserializer, NullInboundAdapter)
            : Consume(token, mediator, BinaryDeserializer, _adapter);
    }

    private async Task Consume<TPayload>(
        CancellationToken token,
        IMediatorAdapter mediator,
        INatsDeserialize<TPayload> deserializer,
        IInboundAdapter<TPayload, TMessage> adapter)
    {
        var messages = await _toolbox.ConsumeMany(token, _stream, _consumer, deserializer);
        await foreach (var message in messages.WithCancellation(token))
            await ConsumeOne(token, message, adapter, mediator);
    }

    protected virtual async Task ConsumeOne<TPayload>(
        CancellationToken token, NatsJSMsg<TPayload> message,
        IInboundAdapter<TPayload, TMessage> adapter,
        IMediatorAdapter mediator)
    {
        try
        {
            var content = Unpack(message, adapter);
            var done = mediator.ExecuteHandler(content, token);
            await message.WaitAndKeepAlive(token, done);
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to process message");
        }
    }
    
    protected TMessage Unpack<TPayload>(
        NatsJSMsg<TPayload> message, IInboundAdapter<TPayload, TMessage> adapter) => 
        _toolbox.Unpack(message, adapter);

    public virtual void Dispose() => _disposables.Dispose();
}
