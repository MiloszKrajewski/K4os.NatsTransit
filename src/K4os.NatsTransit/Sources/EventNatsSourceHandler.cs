using K4os.Async.Toys;
using K4os.NatsTransit.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Sources;

public class EventNatsSourceHandler<TEvent>:
    INatsSourceHandler
    where TEvent: IRequest
{
    protected readonly ILogger Log;
    
    private readonly NatsToolbox _toolbox;
    private readonly string _stream;
    private readonly string _consumer;
    private readonly INatsDeserialize<TEvent> _requestDeserializer;

    public record Config(string Stream, string Consumer): INatsSourceConfig
    {
        public INatsSourceHandler CreateHandler(NatsToolbox toolbox) =>
            new EventNatsSourceHandler<TEvent>(toolbox, this);
    }

    public EventNatsSourceHandler(NatsToolbox toolbox, Config config)
    {
        Log = toolbox.GetLogger(this);
        _toolbox = toolbox;
        _stream = config.Stream;
        _consumer = config.Consumer;
        _requestDeserializer = toolbox.Deserializer<TEvent>();
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
        NatsJSMsg<TEvent> message, MessageHandler handler, CancellationToken token)
    {
        try
        {
            message.EnsureSuccess();
            var request = message.Data!;
            var response = TryExecuteHandler(handler, request, token);
            await _toolbox.WaitAndKeepAlive(message, response, token);
            await response;
            await message.AckAsync(null, token);
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to process message");
        }
    }

    private static Task TryExecuteHandler(
        MessageHandler handler, TEvent request, CancellationToken token) =>
        Task.Run(() => handler(request, token), token);

    public void Dispose() { }
}
