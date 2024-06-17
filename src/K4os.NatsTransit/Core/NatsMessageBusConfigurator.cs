using System.Reflection;
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
    private string _applicationName = GetApplicationName();

    private readonly List<INatsContextAction> _actions = [];
    private readonly List<INatsTargetConfig> _targets = [];
    private readonly List<INatsSourceConfig> _sources = [];

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
    
    private static string GetApplicationName()
    {
        var topAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(topAssemblyName);
        return topAssemblyName.Replace(".", "-").ToLower();
    }

    private void Application(string name) => _applicationName = name;
    public string ApplicationName => _applicationName;

    private void ContextAction(Func<INatsJSContext, Task> action) => 
        _actions.Add(new NatsContextAction(t => action(t.JetStream)));

    private void ContextAction(Func<INatsJSContext, ValueTask> action) => 
        ContextAction(js => action(js).AsTask());
    
    private void ContextAction<T>(Func<INatsJSContext, ValueTask<T>> action) => 
        ContextAction(js => action(js).AsTask());

    public void Stream(string stream, string[] subjects) => 
        ContextAction(js => js.CreateStreamAsync(new StreamConfig(stream, subjects)));
    
    public void Consumer(
        string stream, string consumer, bool applicationSuffix, string[]? subjects = null)
    {
        if (applicationSuffix)
            consumer = $"{consumer}-{_applicationName}";
        ContextAction(
            context => context.CreateOrUpdateConsumerAsync(
                stream, new ConsumerConfig {
                    Name = consumer,
                    DurableName = consumer,
                    FilterSubjects = subjects,
                    AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    AckWait = NatsConstants.AckTimeout,
                }));
    }
    
    public void CommandConsumer(
        string stream, string? consumer = null, 
        string[]? subjects = null) =>
        Consumer(stream, consumer ?? "commands", false, subjects);
    
    public void EventConsumer(
        string stream, string? consumer = null, bool applicationSuffix = true, 
        string[]? subjects = null) =>
        Consumer(stream, consumer ?? "events", applicationSuffix, subjects);

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
    
    public void CommandSource<TCommand>(string stream, string consumer) where TCommand: IRequest =>
        _sources.Add(new CommandNatsSourceHandler<TCommand>.Config(stream, consumer));
    
    public void EventSource<TEvent>(string stream, string consumer) where TEvent: INotification =>
        _sources.Add(new EventNatsSourceHandler<TEvent>.Config(stream, consumer));
}