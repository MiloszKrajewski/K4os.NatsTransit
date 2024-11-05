using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Extensions;
using MediatR;

namespace K4os.NatsTransit.Configuration;

public class FluentNats: IFluentNats
{
    private readonly NatsMessageBusConfigurator _configurator;

    public FluentNats(NatsMessageBusConfigurator configurator)
    {
        _configurator = configurator;
    }

    public INatsMessageBusConfigurator Configurator => _configurator;

    public IFluentNatsTopic WithTopic(string name, StreamType streamType = StreamType.Default)
    {
        string?[] subjects = [
            $"{name}.commands.>",
            streamType == StreamType.Commands ? null : $"{name}.events.>",
            $"{name}.requests.>"
        ];
        _configurator.Stream(name, subjects.SelectNotNull().ToArray(), streamType);
        return new FluentNatsTopic(_configurator, name, streamType);
    }
}

public class FluentNatsTopic: IFluentNatsTopic
{
    private readonly NatsMessageBusConfigurator _configurator;
    private readonly string _topicName;
    private readonly bool _eventsAllowed;

    public FluentNatsTopic(NatsMessageBusConfigurator configurator, string topicName, StreamType streamType)
    {
        _configurator = configurator;
        _topicName = topicName;
        _eventsAllowed = streamType != StreamType.Commands;
    }

    public IFluentNatsTopic EmitsEvents<TEvent>(string eventName) where TEvent: INotification
    {
        if (!_eventsAllowed)
            throw new InvalidOperationException("Cannot emit events on command-only topic.");

        _configurator.EventTarget<TEvent>($"{_topicName}.events.{eventName}");
        return this;
    }

    public IFluentNatsTopic SendsCommands<TCommand>(string commandName) where TCommand: IRequest
    {
        _configurator.CommandTarget<TCommand>($"{_topicName}.commands.{commandName}");
        return this;
    }

    public IFluentNatsTopic SendsRequests<TRequest, TResponse>(string requestName) where TRequest: IRequest<TResponse>
    {
        _configurator.RequestTarget<TRequest, TResponse>($"{_topicName}.requests.{requestName}");
        return this;
    }

    public IFluentNatsTopic SendsQueries<TRequest, TResponse>(
        string queryName)
        where TRequest: IRequest<TResponse>
    {
        _configurator.QueryTarget<TRequest, TResponse>($"{_topicName}.queries.{queryName}");
        return this;
    }

    public IFluentNatsTopic ConsumesCommands(
        string consumer, string[] commandNames, int concurrency)
    {
        if (commandNames is null or { Length: 0 })
            throw new ArgumentException("At least one command name must be provided.");

        var subjects = commandNames.Select(cn => $"{_topicName}.commands.{cn}").ToArray();
        _configurator.Consumer(_topicName, consumer, false, subjects);
        _configurator.CommandSource<IRequest>(_topicName, consumer, false, null, concurrency);
        return this;
    }

    public IFluentNatsTopic ConsumesEvents(string consumer, string[] eventNames, int concurrency)
    {
        if (!_eventsAllowed)
            throw new InvalidOperationException("Cannot emit events on command-only topic.");

        if (eventNames is null or { Length: 0 })
            throw new ArgumentException("At least one event name must be provided.");

        var subjects = eventNames.Select(en => $"{_topicName}.events.{en}").ToArray();
        _configurator.Consumer(_topicName, consumer, true, subjects);
        _configurator.EventSource<INotification>(_topicName, consumer, true, null, concurrency);
        return this;
    }

    public IFluentNatsTopic ObservesEvents(string[] eventNames)
    {
        if (!_eventsAllowed)
            throw new InvalidOperationException("Cannot emit events on command-only topic.");
        
        if (eventNames is null or { Length: 0 })
            throw new ArgumentException("At least one event name must be provided.");

        var subjects = eventNames.Select(en => $"{_topicName}.events.{en}");
        foreach (var subject in subjects) _configurator.EventListener<INotification>(subject);
        return this;
    }

    public IFluentNatsTopic RespondsToQueries<TRequest, TResponse>(
        string queryName, int concurrency = 1)
        where TRequest: IRequest<TResponse>
    {
        var subject = $"{_topicName}.queries.{queryName}";
        _configurator.QuerySource<TRequest, TResponse>(subject, null, null, concurrency);
        return this;
    }

    public IFluentNatsTopic HandlesRequests<TRequest, TResponse>(
        string consumer, string requestName, int concurrency = 1)
        where TRequest: IRequest<TResponse>
    {
        var subject = $"{_topicName}.requests.{requestName}";
        _configurator.Consumer(_topicName, consumer, false, [subject]);
        _configurator.RequestSource<TRequest, TResponse>(subject, consumer, false, null, null, concurrency);
        return this;
    }
}
