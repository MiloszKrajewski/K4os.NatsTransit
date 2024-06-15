using K4os.Async.Toys;
using K4os.NatsTransit.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace K4os.NatsTransit.Sources;

public class QueryNatsSourceHandler<TRequest, TResponse>:
    INatsSourceHandler
    where TRequest: IRequest<TResponse>
{
    protected readonly ILogger Log;
    
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly INatsDeserialize<TRequest> _requestDeserializer;
    private readonly INatsSerialize<TResponse> _responseSerializer;

    public record Config(string Subject): INatsSourceConfig
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
    }

    public IDisposable Subscribe(CancellationToken token, MessageHandler handler) => 
        Agent.Launch(c => Consume(c, handler), Log, token);

    private async Task Consume(IAgentContext context, MessageHandler handler)
    {
        var token = context.Token;
        var consumer = _toolbox.SubscribeMany(token, _subject, _requestDeserializer);
        
        await foreach (var message in consumer)
        {
            await ConsumeOne(message, handler, token);
        }
    }

    private async Task ConsumeOne(
        NatsMsg<TRequest> message, MessageHandler handler, CancellationToken token)
    {
        try
        {
            message.EnsureSuccess();
            var request = message.Data!;
            var result = TryExecuteHandler(handler, request, token);
            await TrySendResponse(message, await result, token);
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to process message");
        }
    }

    private async Task TrySendResponse(
        NatsMsg<TRequest> message, Result result, CancellationToken token)
    {
        try
        {
            var responseSent = result switch {
                { Error: { } e } => _toolbox.Respond(token, message, e),
                { Response: var r } => _toolbox.Respond(token, message, r!, _responseSerializer),
                _ => Task.CompletedTask
            };
            await responseSent;
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to send response");
        }
    }

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