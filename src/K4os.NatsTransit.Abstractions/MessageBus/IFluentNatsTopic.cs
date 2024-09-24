using MediatR;

namespace K4os.NatsTransit.Abstractions.MessageBus;

public interface IFluentNatsTopic
{
    IFluentNatsTopic EmitsEvents<TEvent>(
        string eventName) where TEvent: INotification;

    IFluentNatsTopic SendsCommands<TCommand>(
        string commandName) where TCommand: IRequest;
    
    IFluentNatsTopic SendsQueries<TRequest, TResponse>(string queryName)
        where TRequest: IRequest<TResponse>;

    IFluentNatsTopic ConsumesCommands(
        string consumer, string[] commandNames, int concurrency);

    IFluentNatsTopic ConsumesEvents(
        string consumer, string[] eventNames, int concurrency);
    
    IFluentNatsTopic ObservesEvents(
        string[] eventNames);

    IFluentNatsTopic RespondsToQueries<TRequest, TResponse>(string queryName, int concurrency = 1) 
        where TRequest: IRequest<TResponse>;
}

public static class FluentNatsTopicExtensions
{
    public static IFluentNatsTopic ConsumesAllCommands(
        this IFluentNatsTopic topic, int concurrency = 1) =>
        topic.ConsumesCommands("commands", [">"], concurrency);

    public static IFluentNatsTopic ConsumesEvents(
        this IFluentNatsTopic topic, string[] eventNames, int concurrency = 1) =>
        topic.ConsumesEvents("events", eventNames, concurrency);
    
    public static IFluentNatsTopic ConsumesEvents(
        this IFluentNatsTopic topic, string eventName, int concurrency = 1) =>
        topic.ConsumesEvents("events", [eventName], concurrency);
    
    public static IFluentNatsTopic ConsumesAllEvents(
        this IFluentNatsTopic topic, int concurrency = 1) =>
        topic.ConsumesEvents("events", [">"], concurrency);

    public static IFluentNatsTopic ObservesEvents(
        this IFluentNatsTopic topic, string eventName) =>
        topic.ObservesEvents([eventName]);
}
