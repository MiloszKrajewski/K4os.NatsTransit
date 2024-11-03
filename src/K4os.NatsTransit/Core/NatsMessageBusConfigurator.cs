using System.Reflection;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Sources;
using K4os.NatsTransit.Targets;
using MediatR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace K4os.NatsTransit.Core;

public class NatsMessageBusConfigurator: INatsMessageBusConfigurator
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
        IExceptionSerializer? exceptionSerializer,
        IMessageDispatcher dispatcher,
        INatsMessageTracer? messageTracer) =>
        new(
            loggerFactory,
            connection, context,
            serializerFactory,
            exceptionSerializer,
            dispatcher,
            messageTracer,
            _actions, _targets, _sources);

    private static string GetApplicationName()
    {
        var topAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(topAssemblyName);
        return topAssemblyName.Replace(".", "-").ToLower();
    }
    
    public void Application(string name) => _applicationName = name;
    public string ApplicationName => _applicationName;

    private void ContextAction(Func<INatsJSContext, Task> action) =>
        _actions.Add(new NatsContextAction(t => action(t.JetStream)));

    private void ContextAction<T>(Func<INatsJSContext, ValueTask<T>> action) =>
        ContextAction(js => action(js).AsTask());
    
    private void Stream(
        string stream, string[] subjects, 
        Action<StreamConfig>? configure)
    {
        var config = new StreamConfig(stream, subjects) {
            Retention = StreamConfigRetention.Interest,
        };
        configure?.Invoke(config);
        ContextAction(js => js.CreateStreamAsync(config));
    }

    public void Stream(string stream, string[] subjects) => Stream(stream, subjects, null);

    private void Consumer(
        string stream, string consumer, 
        string? suffix, string[]? subjects, 
        Action<ConsumerConfig>? configure)
    {
        consumer = suffix is null ? consumer : $"{consumer}-{suffix}";
        var config = new ConsumerConfig {
            Name = consumer,
            DurableName = consumer,
            FilterSubjects = subjects,
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            AckWait = NatsConstants.AckTimeout,
            MaxDeliver = 10,
        };
        configure?.Invoke(config);
        ContextAction(context => context.CreateOrUpdateConsumerAsync(stream, config));
    }
    
    private void Consumer(
        string stream, string consumer, string? suffix = null, string[]? subjects = null) =>
        Consumer(stream, consumer, suffix, subjects, null);

    public void Consumer(
        string stream, string consumer, string[]? subjects = null) =>
        Consumer(stream, consumer, null, subjects);
    
    public void Consumer(
        string stream, string consumer, bool applicationSuffix, string[]? subjects = null) =>
        Consumer(stream, consumer, applicationSuffix ? _applicationName : null, subjects);

    public void QueryTarget<TRequest, TResponse>(
        string subject,
        TimeSpan? timeout = null,
        OutboundAdapter<TRequest>? outboundAdapter = null,
        InboundAdapter<TResponse>? inboundAdapter = null)
        where TRequest: IRequest<TResponse> =>
        _targets.Add(
            new QueryNatsTargetHandler<TRequest, TResponse>.Config(
                subject, timeout, outboundAdapter, inboundAdapter));

    public void RequestTarget<TRequest, TResponse>(
        string subject,
        TimeSpan? timeout = null,
        OutboundAdapter<TRequest>? outboundAdapter = null,
        InboundAdapter<TResponse>? inboundAdapter = null)
        where TRequest: IRequest<TResponse> =>
        _targets.Add(
            new RequestNatsTargetHandler<TRequest, TResponse>.Config(
                subject, timeout, outboundAdapter, inboundAdapter));

    public void CommandTarget<TCommand>(
        string subject,
        OutboundAdapter<TCommand>? outboundAdapter = null)
        where TCommand: IRequest =>
        _targets.Add(new CommandNatsTargetHandler<TCommand>.Config(subject, outboundAdapter));

    public void EventTarget<TEvent>(
        string subject,
        OutboundAdapter<TEvent>? outboundAdapter = null)
        where TEvent: INotification =>
        _targets.Add(new EventNatsTargetHandler<TEvent>.Config(subject, outboundAdapter));

    public void QuerySource<TRequest, TResponse>(
        string subject,
        InboundAdapter<TRequest>? inboundAdapter = null,
        OutboundAdapter<TResponse>? outboundAdapter = null,
        int concurrency = 1)
        where TRequest: IRequest<TResponse> =>
        _sources.Add(
            new QueryNatsSourceHandler<TRequest, TResponse>.Config(
                subject, inboundAdapter, outboundAdapter, concurrency));

    public void RequestSource<TRequest, TResponse>(
        string stream,
        string consumer,
        bool applicationSuffix = false,
        InboundAdapter<TRequest>? inboundAdapter = null,
        OutboundAdapter<TResponse>? outboundAdapter = null,
        int concurrency = 1)
        where TRequest: IRequest<TResponse> =>
        _sources.Add(
            new RequestNatsSourceHandler<TRequest, TResponse>.Config(
                stream, ConsumerName(consumer, applicationSuffix), 
                inboundAdapter, outboundAdapter, concurrency));

    public void CommandSource<TCommand>(
        string stream, string consumer,
        bool applicationSuffix = false,
        InboundAdapter<TCommand>? inboundAdapter = null,
        int concurrency = 1)
        where TCommand: IRequest =>
        _sources.Add(
            new CommandNatsSourceHandler<TCommand>.Config(
                stream, ConsumerName(consumer, applicationSuffix), 
                inboundAdapter, concurrency));

    public void EventSource<TEvent>(
        string stream, string consumer,
        bool applicationSuffix = true,
        InboundAdapter<TEvent>? inboundAdapter = null,
        int concurrency = 1
    ) where TEvent: INotification =>
        _sources.Add(
            new EventNatsSourceHandler<TEvent>.Config(
                stream, ConsumerName(consumer, applicationSuffix), 
                inboundAdapter, concurrency));

    public void EventListener<TEvent>(
        string subject,
        InboundAdapter<TEvent>? inboundAdapter = null,
        int concurrency = 1) where TEvent: INotification =>
        _sources.Add(
            new EventNatsListenerHandler<TEvent>.Config(
                subject, inboundAdapter, concurrency));


    private string ConsumerName(string consumer, bool applicationSuffix) => 
        applicationSuffix ? $"{consumer}-{_applicationName}" : consumer;
}