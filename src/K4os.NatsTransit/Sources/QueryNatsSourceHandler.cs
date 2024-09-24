using System.Diagnostics;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
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
    private readonly OutboundPair<TResponse> _serializer;

    public record Config(
        string Subject,
        InboundPair<TRequest>? InboundPair = null,
        OutboundPair<TResponse>? OutboundPair = null,
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
        var deserializer = config.InboundPair ?? toolbox.Deserializer<TRequest>();
        _serializer = config.OutboundPair ?? toolbox.Serializer<TResponse>();
        _consumer = NatsSubscriber.Create(toolbox, subject, this, deserializer);
    }

    private static string GetActivityName(Config config)
    {
        var requestType = typeof(TRequest).Name;
        var responseType = typeof(TResponse).Name;
        var subject = config.Subject;
        return $"Subscribe<{requestType},{responseType}>({subject})";
    }

    public IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher) => 
        _consumer.Subscribe(token, dispatcher, _concurrency);

    public Activity? OnTrace(IMessageDispatcher context, NatsHeaders? headers) =>
        _toolbox.ReceiveActivity(_activityName, headers, true);

    public Task<Result<TResponse>> OnHandle<TPayload>(
        CancellationToken token, IMessageDispatcher dispatcher, 
        NatsMsg<TPayload> payload, TRequest message) =>
        dispatcher.ForkDispatchWithResult<TRequest, TResponse>(message, token);

    public async Task OnSuccess<TPayload>(
        CancellationToken token, IMessageDispatcher context, 
        NatsMsg<TPayload> payload, TRequest request, Result<TResponse> response)
    {
        try
        {
            var sent = response switch {
                { Error: { } e } => _toolbox.Respond(token, payload, e),
                { Value: { } r } => _toolbox.Respond(token, payload, r, _serializer), // no responder implemented yet
                _ => default
            };
            await sent;
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to send response");
        }
    }

    public Task OnFailure<TPayload>(
        CancellationToken token, IMessageDispatcher context, 
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
