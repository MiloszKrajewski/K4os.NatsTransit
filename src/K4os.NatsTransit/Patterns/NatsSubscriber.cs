using System.Diagnostics;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

// protected virtual Activity? OnTrace(NatsHeaders? headers) => null;
// // _toolbox.ReceiveActivity(_activityName, message.Headers, _hasResponse);
//
// protected abstract Task<TResponse?> OnHandle<TPayload>(
//     CancellationToken token, TContext context, 
//     NatsMsg<TPayload> payload, TRequest message);
//
// protected virtual Task OnSuccess<TPayload>(
//     CancellationToken token, TContext context, 
//     NatsMsg<TPayload> payload, TRequest request, TResponse? response) =>
//     Task.CompletedTask;
//
// protected virtual Task OnFailure<TPayload>(
//     CancellationToken token, TContext context, NatsMsg<TPayload> payload, Exception error) =>
//     Task.CompletedTask;

// public IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher) =>
//     Subscribe(token, dispatcher, _concurrency);
//
// protected override Task<TResponse?> OnHandle<TPayload>(
//     CancellationToken token, IMessageDispatcher dispatcher, NatsMsg<TPayload> payload, TRequest message) => 
//     dispatcher.ForkDispatch<TRequest, TResponse>(message, token);
//
// protected override Task OnFailure<TPayload>(
//     CancellationToken token, IMessageDispatcher context, NatsMsg<TPayload> payload, Exception error)
// {
//     Log.LogError(error, "Failed to process message");
//     return Task.CompletedTask;
// }

public static class NatsSubscriber
{
    public static NatsSubscriber<TContext, TRequest, TResponse> Create<TContext, TRequest, TResponse>(
        NatsToolbox toolbox,
        string subject,
        NatsSubscriber<TContext, TRequest, TResponse>.IEvents events,
        InboundPair<TRequest> deserializer)
        where TRequest: notnull =>
        new(toolbox, subject, events, deserializer);
}

public class NatsSubscriber<TContext, TRequest, TResponse>
    where TRequest: notnull
{
    protected readonly ILogger Log;
    protected NatsToolbox Toolbox => _toolbox;

    private readonly DisposableBag _disposables;

    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly IEvents _events;
    private readonly Func<CancellationToken, TContext, Task> _consumer;

    public interface IEvents
    {
        Activity? OnTrace(TContext context, NatsHeaders? headers);

        Task<TResponse?> OnHandle<TPayload>(
            CancellationToken token, TContext context,
            NatsMsg<TPayload> payload, TRequest message);

        Task OnSuccess<TPayload>(
            CancellationToken token, TContext context,
            NatsMsg<TPayload> payload, TRequest request, TResponse? response);

        Task OnFailure<TPayload>(
            CancellationToken token, TContext context,
            NatsMsg<TPayload> payload, Exception error);
    }

    public NatsSubscriber(
        NatsToolbox toolbox,
        string subject,
        IEvents events,
        InboundPair<TRequest> deserializer)
    {
        Log = toolbox.GetLoggerFor(this);
        _disposables = new DisposableBag();
        _toolbox = toolbox;
        _subject = subject;
        _events = events;
        _consumer = deserializer.Unpack() switch {
            (var (s, a), null) => (t, c) => Loop(t, c, s, a),
            (null, var (s, a)) => (t, c) => Loop(t, c, s, a),
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

    private async Task Loop<TPayload>(
        CancellationToken token,
        TContext context,
        INatsDeserialize<TPayload> deserializer,
        IInboundAdapter<TPayload, TRequest> adapter)
    {
        var messages = _toolbox.SubscribeMany(token, _subject, deserializer);
        await foreach (var message in messages)
        {
            using var _ = _events.OnTrace(context, message.Headers);
            try
            {
                var request = _toolbox.Unpack(message, adapter);
                var done = _events.OnHandle(token, context, message, request);
                var response = await done;
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
