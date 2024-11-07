using System.Diagnostics;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Serialization;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Sources;

public class QueryNatsSourceHandler<TRequest, TResponse>:
    NatsSubscriber<IMessageDispatcher, TRequest, Result<TResponse>>.IEvents,
    INatsSourceHandler
    where TRequest: IRequest<TResponse>
{
    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;
    private readonly string _activityName;
    private readonly string _requestType;
    private readonly int _concurrency;
    private readonly NatsSubscriber<IMessageDispatcher, TRequest, Result<TResponse>> _consumer;
    private readonly NatsResponder<TResponse> _responder;

    public record Config(
        string Subject,
        InboundAdapter<TRequest>? InboundAdapter = null,
        OutboundAdapter<TResponse>? OutboundAdapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new QueryNatsSourceHandler<TRequest, TResponse>(toolbox, this);
    }

    public QueryNatsSourceHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLoggerFor(this);
        _toolbox = toolbox;
        _activityName = GetActivityName(config);
        _requestType = typeof(TRequest).GetFriendlyName();
        _concurrency = config.Concurrency.NotLessThan(1);
        var subject = config.Subject;
        var deserializer = config.InboundAdapter ?? toolbox.GetInboundAdapter<TRequest>();
        var serializer = config.OutboundAdapter ?? toolbox.GetOutboundAdapter<TResponse>();
        _consumer = NatsSubscriber.Create(toolbox, subject, this, deserializer);
        _responder = NatsResponder.Create(toolbox, serializer);
    }

    private static string GetActivityName(Config config)
    {
        var subject = config.Subject;
        return $"Query({subject}).Subscribe";
    }

    public IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher) => 
        _consumer.Subscribe(token, dispatcher, _concurrency);

    public Activity? OnTrace(IMessageDispatcher context, NatsHeaders? headers) =>
        _toolbox.Tracing.ReceivedScope(_activityName, headers, true);

    public Task<Result<TResponse>> OnHandle<TPayload>(
        CancellationToken token, Activity? activity, IMessageDispatcher dispatcher,
        NatsMsg<TPayload> payload, TRequest message) =>
        _toolbox.Metrics.HandleScope(
            payload.Subject, 
            () => dispatcher.ForkDispatchWithResult<TRequest, TResponse>(message, token));

    public async Task OnSuccess<TPayload>(
        CancellationToken token, Activity? activity, IMessageDispatcher dispatcher, 
        NatsMsg<TPayload> payload, TRequest request, Result<TResponse> response)
    {
        try
        {
            var sent = response switch {
                { Error: { } e } => Respond(token, activity, payload, e),
                { Value: { } r } => Respond(token, activity, payload, r),
                _ => default
            };
            await sent;
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to send response");
        }
    }

    private ValueTask Respond<TPayload>(
        CancellationToken token, Activity? activity, 
        NatsMsg<TPayload> request, Exception error) => 
        _responder.Respond(token, activity, request, error);

    private ValueTask Respond<TPayload>(
        CancellationToken token, Activity? activity, 
        NatsMsg<TPayload> request, TResponse response) => 
        _responder.Respond(token, activity, request, response);

    public Task OnFailure<TPayload>(
        CancellationToken token, Activity? activity, IMessageDispatcher dispatcher, 
        NatsMsg<TPayload> payload, Exception error)
    {
        Log.LogError(error, "Failed to process request {RequestType} in {ActivityName}", _requestType, _activityName);
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
