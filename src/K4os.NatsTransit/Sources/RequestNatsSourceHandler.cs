using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Sources;

public class RequestNatsSourceHandler<TRequest, TResponse>:
    NatsConsumeSourceHandler<TRequest>
    where TRequest: IRequest<TResponse>
{
    private readonly INatsSerialize<TResponse> _responseSerializer;
    private readonly IOutboundAdapter<TResponse>? _outboundAdapter;

    public record Config(
        string Stream,
        string Consumer,
        IInboundAdapter<TRequest>? InboundAdapter = null,
        IOutboundAdapter<TResponse>? OutboundAdapter = null,
        int Concurrency = 1
    ): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new RequestNatsSourceHandler<TRequest, TResponse>(toolbox, this);
    }

    public RequestNatsSourceHandler(NatsToolbox toolbox, Config config):
        base(
            toolbox, 
            config.Stream, config.Consumer, GetActivityName(config), 
            config.InboundAdapter, 
            config.Concurrency)
    {
        _responseSerializer = toolbox.Serializer<TResponse>();
        _outboundAdapter = config.OutboundAdapter;
    }

    private static string GetActivityName(Config config)
    {
        var requestType = typeof(TRequest).Name;
        var responseType = typeof(TResponse).Name;
        var streamName = config.Stream;
        var consumerName = config.Consumer;
        return $"Consume<{requestType},{responseType}>({streamName}/{consumerName}))";
    }

    protected override async Task ConsumeOne<TPayload>(
        CancellationToken token,
        NatsJSMsg<TPayload> message,
        IInboundAdapter<TPayload, TRequest> adapter,
        IMessageDispatcher mediator)
    {
        try
        {
            var request = Unpack(message, adapter);
            var result = mediator.ForkDispatchWithResult<TRequest, TResponse>(request, token);
            await message.WaitAndKeepAlive(token, result);
            await TrySendResponse(message, await result, token);
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to process message");
        }
    }

    private async Task TrySendResponse<TPayload>(
        NatsJSMsg<TPayload> message, Result<TResponse> result, CancellationToken token)
    {
        var toolbox = Toolbox;
        try
        {
            var task = result switch {
                { Error: { } e } => SendResponse(token, toolbox, message, e).AsTask(),
                { Value: { } r } => SendResponse(token, toolbox, message, r).AsTask(),
                _ => Task.CompletedTask
            };
            await task;
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to send response");
        }
    }

    private ValueTask SendResponse<TPayload>(
        CancellationToken token,
        NatsToolbox toolbox,
        NatsJSMsg<TPayload> request,
        TResponse response) =>
        _outboundAdapter is null
            ? toolbox.Respond(token, request, response, _responseSerializer)
            : toolbox.Respond(token, request, response, _outboundAdapter);

    private ValueTask SendResponse<TPayload>(
        CancellationToken token,
        NatsToolbox toolbox,
        NatsJSMsg<TPayload> request,
        Exception response) =>
        toolbox.Respond(token, request, response);
}
