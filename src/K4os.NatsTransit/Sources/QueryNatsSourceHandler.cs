using System.Buffers;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Sources;

public class QueryNatsSourceHandler<TRequest, TResponse>:
    INatsSourceHandler
    where TRequest: IRequest<TResponse>
{
    protected readonly ILogger Log;
    
    public static INatsDeserialize<IMemoryOwner<byte>> BinaryDeserializer =>
        NatsRawSerializer<IMemoryOwner<byte>>.Default;

    public static IInboundAdapter<TRequest, TRequest> NullInboundAdapter => 
        NullInboundAdapter<TRequest>.Default;
    
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly INatsDeserialize<TRequest> _requestDeserializer;
    private readonly INatsSerialize<TResponse> _responseSerializer;
    private readonly IInboundAdapter<TRequest>? _inboundAdapter;
    private readonly IOutboundAdapter<TResponse>? _outboundAdapter;
    private readonly int _concurrency;
    private readonly DisposableBag _disposables;
    private readonly string _activityName;

    public record Config(
        string Subject,
        IInboundAdapter<TRequest>? InboundAdapter = null,
        IOutboundAdapter<TResponse>? OutboundAdapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new QueryNatsSourceHandler<TRequest, TResponse>(toolbox, this);
    }

    public QueryNatsSourceHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _subject = config.Subject;
        _requestDeserializer = toolbox.Deserializer<TRequest>();
        _responseSerializer = toolbox.Serializer<TResponse>();
        _inboundAdapter = config.InboundAdapter;
        _outboundAdapter = config.OutboundAdapter;
        _concurrency = config.Concurrency.NotLessThan(1);
        _disposables = new DisposableBag();
        var requestType = typeof(TRequest).Name;
        var responseType = typeof(TResponse).Name;
        _activityName = $"Subscribe<{requestType},{responseType}>({_subject})";
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


    protected async Task ConsumeOne<TPayload>(
        CancellationToken token, 
        NatsMsg<TPayload> message, 
        IInboundAdapter<TPayload, TRequest> adapter, 
        IMessageDispatcher mediator)
    {
        using var _ = _toolbox.ReceiveActivity(_activityName, message.Headers);
        try
        {
            var request = Unpack(message, adapter);
            var result = mediator.ForkDispatchWithResult<TRequest, TResponse>(request, token);
            await TrySendResponse(message, await result, token);
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to process message");
        }
    }

    private TRequest Unpack<TPayload>(
        NatsMsg<TPayload> message, IInboundAdapter<TPayload, TRequest> adapter) => 
        _toolbox.Unpack(message, adapter);

    private async Task TrySendResponse<TPayload>(
        NatsMsg<TPayload> message, Result<TResponse> result, CancellationToken token)
    {
        try
        {
            var sent = result switch {
                { Error: { } e } => SendResponse(token, _toolbox, message, e).AsTask(),
                { Value: { } r } => SendResponse(token, _toolbox, message, r).AsTask(),
                _ => Task.CompletedTask
            };
            await sent;
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to send response");
        }
    }

    private ValueTask SendResponse<TPayload>(
        CancellationToken token,
        NatsToolbox toolbox,
        NatsMsg<TPayload> request,
        TResponse response) =>
        _outboundAdapter is null
            ? toolbox.Respond(token, request, response, _responseSerializer)
            : toolbox.Respond(token, request, response, _outboundAdapter);
    
    private ValueTask SendResponse<TPayload>(
        CancellationToken token,
        NatsToolbox toolbox,
        NatsMsg<TPayload> request,
        Exception response) =>
        toolbox.Respond(token, request, response);

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
