using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Sources;

public class RequestNatsSourceHandler<TRequest, TResponse>:
    NatsConsumer<IMessageDispatcher, TRequest, Result<TResponse>>.IEvents,
    INatsSourceHandler
    where TRequest: IRequest<TResponse>
{
    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;
    private readonly string _activityName;
    private readonly string _requestType;
    private readonly int _concurrency;
    private readonly NatsConsumer<IMessageDispatcher, TRequest, Result<TResponse>> _consumer;
    private readonly NatsPublisher<TResponse> _responder;

    public record Config(
        string Stream, string Consumer,
        InboundAdapter<TRequest>? InboundPair = null,
        OutboundAdapter<TResponse>? OutboundPair = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new RequestNatsSourceHandler<TRequest, TResponse>(toolbox, this);
    }

    public RequestNatsSourceHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLoggerFor(this);
        _toolbox = toolbox;
        _activityName = GetActivityName(config);
        _requestType = typeof(TRequest).GetFriendlyName();
        _concurrency = config.Concurrency.NotLessThan(1);
        var streamName = config.Stream;
        var consumerName = config.Consumer;
        var deserializer = config.InboundPair ?? toolbox.Deserializer<TRequest>();
        var serializer = config.OutboundPair ?? toolbox.Serializer<TResponse>();
        _consumer = NatsConsumer.Create(toolbox, streamName, consumerName, this, deserializer);
        _responder = NatsPublisher.Create(toolbox, serializer); 
    }

    private static string GetActivityName(Config config)
    {
        var requestType = typeof(TRequest).GetFriendlyName();
        var responseType = typeof(TResponse).GetFriendlyName();
        var streamName = config.Stream;
        var consumerName = config.Consumer;
        return $"Consume<{requestType},{responseType}>({streamName}/{consumerName}))";
    }

    private static string GetReplyToSubject<TPayload>(NatsJSMsg<TPayload> payload) =>
        payload.TryGetReplyTo() ?? ThrowNoReplyToHeader<string>();

    [DoesNotReturn]
    private static T ThrowNoReplyToHeader<T>() => 
        throw new InvalidOperationException("Message does not have reply-to header");

    public IDisposable Subscribe(CancellationToken token, IMessageDispatcher dispatcher) => 
        _consumer.Subscribe(token, dispatcher, _concurrency);

    public Activity? OnTrace(IMessageDispatcher context, NatsHeaders? headers) => 
        _toolbox.ReceiveActivity(_activityName, headers, true);

    public Task<Result<TResponse>> OnHandle<TPayload>(
        CancellationToken token, IMessageDispatcher dispatcher, 
        NatsJSMsg<TPayload> payload, TRequest message) => 
        dispatcher.ForkDispatchWithResult<TRequest, TResponse>(message, token);

    public async Task OnSuccess<TPayload>(
        CancellationToken token, IMessageDispatcher context, 
        NatsJSMsg<TPayload> payload, TRequest request, Result<TResponse> response)
    {
        var replyTo = GetReplyToSubject(payload);
        try
        {
            var task = response switch {
                { Error: { } e } => _toolbox.Respond(token, replyTo, e),
                { Value: { } r } => _responder.Publish(token, replyTo, r),
                _ => default
            };
            await task;
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to send response");
        }
    }

    public Task OnFailure<TPayload>(
        CancellationToken token, IMessageDispatcher context, 
        NatsJSMsg<TPayload> payload, Exception error)
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
