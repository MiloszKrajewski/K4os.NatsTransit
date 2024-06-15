using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Sources;
using K4os.NatsTransit.Targets;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace K4os.NatsTransit.Core;

public class NatsMessageBusConfigurator: IMessageBusConfigurator
{
    private readonly List<INatsContextAction> _actions = [];
    private readonly List<INatsTargetConfig> _targets = [];
    private readonly List<INatsSourceConfig> _sources = [];
    
    public void QueryTarget<TRequest, TResponse>(string subject)
        where TRequest: IRequest<TResponse> =>
        _targets.Add(new QueryNatsTargetHandler<TRequest, TResponse>.Config(subject));

    public void RequestTarget<TRequest, TResponse>(string subject)
        where TRequest: IRequest<TResponse> =>
        _targets.Add(new RequestNatsTargetHandler<TRequest, TResponse>.Config(subject));

    public void CommandTarget<TCommand>(string subject)
        where TCommand: IRequest =>
        _targets.Add(new CommandNatsTargetHandler<TCommand>.Config(subject));

    public void EventTarget<TEvent>(string subject)
        where TEvent: INotification =>
        _targets.Add(new EventNatsTargetHandler<TEvent>.Config(subject));
    
    public void QuerySource<TRequest, TResponse>(string subject)
        where TRequest: IRequest<TResponse> =>
        _sources.Add(new QueryNatsSourceHandler<TRequest, TResponse>.Config(subject));

    public void RequestSource<TRequest, TResponse>(string stream, string consumer)
        where TRequest: IRequest<TResponse> =>
        _sources.Add(new RequestNatsSourceHandler<TRequest, TResponse>.Config(stream, consumer));

    public NatsMessageBus CreateMessageBus(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext context,
        INatsSerializerFactory serializerFactory,
        IExceptionSerializer exceptionSerializer,
        MessageHandler handler) =>
        new(
            loggerFactory,
            connection, context, 
            serializerFactory, exceptionSerializer, 
            handler, 
            _actions, _targets, _sources);

    // public void CommandSource<TCommand>(string stream, string subject) where TCommand: IRequest =>
    //     _sources.Add(new CommandSourceHandler<TCommand>(stream, subject));
    //
    // public void EventSource<TEvent>(string stream, string subject) where TEvent: INotification =>
    //     _sources.Add(new EventSourceHandler<TEvent>(stream, subject));

    // private async Task Initialize(INatsConnection connection, INatsJSContext context)
    // {
    //     var streams = await CreateStreams(context);
    //     var publisher = CreatePublisher(connection, _targets.ToFrozenSet(), streams);
    // }

    // private Func<object, Task<object>> CreatePublisher(
    //     INatsConnection connection,
    //     FrozenSet<Target> targets,
    //     FrozenDictionary<string, INatsJSStream> streams)
    // {
    //     Target? FindTarget(Type type) => targets.FirstOrDefault(s => s.Request == type);
    //     INatsJSStream? FindStream(string name) => streams.GetValueOrDefault(name);
    //
    //     return async message => {
    //         var target = FindTarget(message.GetType());
    //         if (target is null) throw new InvalidOperationException("No target found for message");
    //
    //         connection.PublishAsync(target.Subject, message, null,)
    //     }
    // }
    //
    // private async Task<FrozenDictionary<string, INatsJSStream>> CreateStreams(
    //     INatsJSContext context)
    // {
    //     var mapping = _streams
    //         .GroupBy(s => s.Name)
    //         .Select(g => new { Name = g.Key, Config = ToStreamConfig(g) })
    //         .Select(async c => new { c.Name, Stream = await context.CreateStreamAsync(c.Config) });
    //     var streams = await Task.WhenAll(mapping);
    //     return streams.ToFrozenDictionary(kv => kv.Name, kv => kv.Stream);
    // }
    //
    // private async Task<object> Publish(INatsJSContext context, object message)
    // {
    //     var target = _targets.FirstOrDefault(t => message.GetType().IsAssignableTo(t.Request));
    //     if (target is null) throw new InvalidOperationException("No target found for message");
    //
    //     var stream = await context.Stream(target.Stream);
    // }
    //
    // private StreamConfig ToStreamConfig(IEnumerable<StreamDef> streams)
    // {
    //     var stream = streams.Single();
    //     var config = new StreamConfig(stream.Name, [$"{stream.Name}.>"]);
    //     return config;
    // }

    private void ContextAction(Func<INatsJSContext, Task> action)
    {
        _actions.Add(new NatsContextAction(t => action(t.JetStream)));
    }

    public void RequestStream(string stream, string[] subjects)
    {
        ContextAction(js => js.CreateStreamAsync(new StreamConfig(stream, subjects)).AsTask());
    }

    public void RequestConsumer(string stream, string? consumer = null)
    {
        consumer ??= "default";
        ContextAction(
            context => context.CreateOrUpdateConsumerAsync(
                stream, new ConsumerConfig {
                    Name = consumer,
                    DurableName = consumer,
                    AckWait = NatsConstants.AckTimeout,
                }).AsTask());
    }
}