using System.Diagnostics;
using K4os.Async.Toys;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Serialization;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

public static class NatsSubscriber
{
    public static NatsSubscriber<TContext, TRequest, TResponse> Create<TContext, TRequest, TResponse>(
        NatsToolbox toolbox,
        string subject,
        NatsSubscriber<TContext, TRequest, TResponse>.IEvents events,
        InboundAdapter<TRequest> deserializer)
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
            CancellationToken token, Activity? activity, TContext context,
            NatsMsg<TPayload> payload, TRequest message);

        Task OnSuccess<TPayload>(
            CancellationToken token, Activity? activity, TContext context,
            NatsMsg<TPayload> payload, TRequest request, TResponse? response);

        Task OnFailure<TPayload>(
            CancellationToken token, Activity? activity, TContext context,
            NatsMsg<TPayload> payload, Exception error);
    }

    public NatsSubscriber(
        NatsToolbox toolbox,
        string subject,
        IEvents events,
        InboundAdapter<TRequest> deserializer)
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
        IInboundTransformer<TPayload, TRequest> transformer)
    {
        var messages = _toolbox.SubscribeMany(token, _subject, deserializer);
        await foreach (var message in messages)
        {
            using var activity = _events.OnTrace(context, message.Headers);
            try
            {
                activity?.OnReceived(message);
                var request = _toolbox.Unpack(message, transformer);
                activity?.OnUnpacked(request);
                var done = _events.OnHandle(token, activity, context, message, request);
                var response = await done;
                await _events.OnSuccess(token, activity, context, message, request, response);
            }
            catch (Exception error)
            {
                activity?.OnException(error);
                await _events.OnFailure(token, activity, context, message, error);
            }
        }
    }

    public virtual void Dispose() => _disposables.Dispose();
}
