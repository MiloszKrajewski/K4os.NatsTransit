using K4os.Async.Toys;
using K4os.NatsTransit.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Sources;

public class RequestNatsSourceHandler<TRequest, TResponse>:
    INatsSourceHandler
    where TRequest: IRequest<TResponse>
{
    protected readonly ILogger Log;
    
    private readonly NatsToolbox _toolbox;
    private readonly string _stream;
    private readonly string _consumer;
    private readonly INatsDeserialize<TRequest> _requestDeserializer;
    private readonly INatsSerialize<TResponse> _responseSerializer;

    public record Config(string Stream, string Consumer): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new RequestNatsSourceHandler<TRequest, TResponse>(toolbox, this);
    }

    public RequestNatsSourceHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _stream = config.Stream;
        _consumer = config.Consumer;
        _requestDeserializer = toolbox.Deserializer<TRequest>();
        _responseSerializer = toolbox.Serializer<TResponse>();
    }

    public IDisposable Subscribe(CancellationToken token, MessageHandler handler) => 
        Agent.Launch(c => Consume(c, handler), Log, token);

    private async Task Consume(IAgentContext context, MessageHandler handler)
    {
        var token = context.Token;
        var consumer = await _toolbox.ConsumeMany(token, _stream, _consumer, _requestDeserializer);
        
        await foreach (var message in consumer.WithCancellation(token))
        {
            await ConsumeOne(message, handler, token);
        }
    }

    private async Task ConsumeOne(
        NatsJSMsg<TRequest> message, MessageHandler handler, CancellationToken token)
    {
        try
        {
            message.EnsureSuccess();
            var request = message.Data!;
            var result = TryExecuteHandler(handler, request, token);
            await _toolbox.WaitAndKeepAlive(message, result, token);
            await TrySendResponse(message, await result, token);
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to process message");
        }
    }

    private async Task TrySendResponse(
        NatsJSMsg<TRequest> message, Result result, CancellationToken token)
    {
        var replyTo = GetReplyToSubject(message);
        if (replyTo is null) return;

        try
        {
            await (result switch {
                { Error: { } e } => _toolbox.Respond(token, replyTo, e),
                { Response: var r } => _toolbox.Respond(token, replyTo, r!, _responseSerializer),
                _ => Task.CompletedTask
            });
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to send response");
        }
    }

    private static string? GetReplyToSubject(NatsJSMsg<TRequest> message) =>
        message.Headers?.TryGetValue(NatsConstants.ReplyToHeaderName, out var value) ?? false
            ? value.ToString()
            : null;

    public record Result(TResponse? Response, Exception? Error);

    private static async Task<Result> TryExecuteHandler(
        MessageHandler handler, TRequest request, CancellationToken token)
    {
        try
        {
            var response = await Task.Run(() => handler(request, token), token);
            return new Result((TResponse?)response, null);
        }
        catch (Exception error)
        {
            return new Result(default, error);
        }
    }

    public void Dispose() { }
}