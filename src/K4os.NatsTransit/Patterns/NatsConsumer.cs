using System.Diagnostics;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Patterns;

public static class NatsConsumer
{
    public static NatsConsumer<TContext, TRequest, TResponse> Create<TContext, TRequest, TResponse>(
        NatsToolbox toolbox,
        string streamName, string consumerName,
        NatsConsumer<TContext, TRequest, TResponse>.IEvents events,
        InboundPair<TRequest> deserializer)
        where TRequest: notnull =>
        new(toolbox, streamName, consumerName, events, deserializer);
}

// private Activity? OnTrace<TPayload>(NatsJSMsg<TPayload> message) => 
//     _toolbox.ReceiveActivity(_activityName, message.Headers, _hasResponse);
//
// protected override Task<TResponse?> OnHandle<TPayload>(
//     CancellationToken token, IMessageDispatcher dispatcher, NatsJSMsg<TPayload> payload, TRequest message) => 
//     dispatcher.ForkDispatch<TRequest, TResponse>(message, token);
//
// protected override Task OnFailure<TPayload>(
//     CancellationToken token, IMessageDispatcher context, NatsJSMsg<TPayload> payload, Exception error)
// {
//     Log.LogError(error, "Failed to process {RequestType} in {ActivityName}", _requestType, ActivityName);
//     return Task.CompletedTask;
// }


public class NatsConsumer<TContext, TRequest, TResponse>
    where TRequest: notnull
{
    protected readonly ILogger Log;
    protected NatsToolbox Toolbox => _toolbox;

    private readonly DisposableBag _disposables;

    private readonly NatsToolbox _toolbox;
    private readonly string _streamName;
    private readonly string _consumerName;
    private readonly IEvents _events;
    private readonly Func<CancellationToken, TContext, Task> _consumer;

    public interface IEvents
    {
        Activity? OnTrace(TContext context, NatsHeaders? headers);

        Task<TResponse?> OnHandle<TPayload>(
            CancellationToken token, TContext context,
            NatsJSMsg<TPayload> payload, TRequest message);

        Task OnSuccess<TPayload>(
            CancellationToken token, TContext context,
            NatsJSMsg<TPayload> payload, TRequest request, TResponse? response);

        Task OnFailure<TPayload>(
            CancellationToken token, TContext context,
            NatsJSMsg<TPayload> payload, Exception error);
    }

    public NatsConsumer(
        NatsToolbox toolbox,
        string streamName, string consumerName,
        IEvents events,
        InboundPair<TRequest> deserializer)
    {
        Log = toolbox.GetLoggerFor(this);
        _disposables = new DisposableBag();
        _toolbox = toolbox;
        _streamName = streamName;
        _consumerName = consumerName;
        _events = events;
        _consumer = deserializer.Unpack() switch {
            (var (s, a), null) => (t, c) => ConsumeLoop(t, c, s, a),
            (null, var (s, a)) => (t, c) => ConsumeLoop(t, c, s, a),
            _ => throw new InvalidOperationException("Misconfigured serialization")
        };
    }

    public IDisposable Subscribe(CancellationToken token, TContext context, int concurrency)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var agents = Enumerable
            .Range(0, concurrency)
            .Select(_ => Agent.Launch(c => _consumer(c.Token, context), Log, cts.Token));
        _disposables.AddMany(agents);
        return Disposable.Create(cts.Cancel);
    }

    private async Task ConsumeLoop<TPayload>(
        CancellationToken token,
        TContext context,
        INatsDeserialize<TPayload> deserializer,
        IInboundAdapter<TPayload, TRequest> adapter)
    {
        var messages = await _toolbox.ConsumeMany(token, _streamName, _consumerName, deserializer);
        await foreach (var message in messages.WithCancellation(token))
        {
            using var _ = _events.OnTrace(context, message.Headers);
            try
            {
                var request = _toolbox.Unpack(message, adapter);
                var done = _events.OnHandle(token, context, message, request);
                await message.WaitAndKeepAliveNoAck(done, token);
                var response = await done;
                await message.AckAsync(null, token);
                await _events.OnSuccess(token, context, message, request, response);
            }
            catch (Exception error)
            {
                await _events.OnFailure(token, context, message, error);
            }
        }
    }

    public virtual void Dispose() => _disposables.Dispose();
}
