using K4os.NatsTransit.Abstractions;
using MediatR;

namespace K4os.NatsTransit.Core;

public class FluentNats: IFluentNats
{
    private readonly NatsMessageBusConfigurator _configurator;

    public FluentNats(NatsMessageBusConfigurator configurator)
    {
        _configurator = configurator;
    }

    public IFluentNatsTopic WithTopic(string name)
    {
        _configurator.Stream(
            name, [$"{name}.commands.>", $"{name}.events.>", $"{name}.requests.>"]);
        return new FluentNatsTopic(_configurator, name);
    }
}

public class FluentNatsTopic: IFluentNatsTopic
{
    private readonly NatsMessageBusConfigurator _configurator;
    private readonly string _topicName;

    public FluentNatsTopic(NatsMessageBusConfigurator configurator, string topicName)
    {
        _configurator = configurator;
        _topicName = topicName;
    }

    public IFluentNatsTopic EmitsEvents<TEvent>(string eventName) where TEvent: INotification
    {
        _configurator.EventTarget<TEvent>($"{_topicName}.events.{eventName}");
        return this;
    }

    public IFluentNatsTopic SendsCommands<TCommand>(string commandName) where TCommand: IRequest
    {
        _configurator.CommandTarget<TCommand>($"{_topicName}.commands.{commandName}");
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
        if (eventNames is null or { Length: 0 })
            throw new ArgumentException("At least one event name must be provided.");

        var subjects = eventNames.Select(en => $"{_topicName}.events.{en}").ToArray();
        _configurator.Consumer(_topicName, consumer, true, subjects);
        _configurator.EventSource<INotification>(_topicName, consumer, true, null, concurrency);
        return this;
    }

    public IFluentNatsTopic ObservesEvents(string[] eventNames)
    {
        if (eventNames is null or { Length: 0 })
            throw new ArgumentException("At least one event name must be provided.");

        var subjects = eventNames.Select(en => $"{_topicName}.events.{en}");
        foreach (var subject in subjects)
            _configurator.EventListener<INotification>(subject);
        return this;
    }

    public IFluentNatsTopic RespondsToQueries<TRequest, TResponse>(
        string queryName, int concurrency = 1)
        where TRequest: IRequest<TResponse>
    {
        _configurator.QuerySource<TRequest, TResponse>(
            $"{_topicName}.queries.{queryName}", null, null, concurrency);
        return this;
    }
}
