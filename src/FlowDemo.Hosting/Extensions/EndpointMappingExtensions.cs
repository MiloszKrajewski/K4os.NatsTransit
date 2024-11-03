using K4os.NatsTransit.Abstractions.MessageBus;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FlowDemo.Hosting.Extensions;

public static class EndpointMappingExtensions
{
    public static RouteHandlerBuilder MapRequest<TRequest, TResponse>(
        this IEndpointRouteBuilder app, string? path = null)
        where TRequest: IRequest<TResponse> =>
        app.MapPost(
            path ?? $"/{typeof(TRequest).Name}",
            async (
                [FromBody] TRequest request,
                [FromServices] IMessageBus messageBus
            ) => Results.Json(await messageBus.Request<TRequest, TResponse>(request))
        );

    public static RouteHandlerBuilder MapQuery<TQuery, TResponse>(
        this IEndpointRouteBuilder app, string? path = null)
        where TQuery: IRequest<TResponse> =>
        app.MapPost(
            path ?? $"/{typeof(TQuery).Name}",
            async (
                [FromBody] TQuery query,
                [FromServices] IMessageBus messageBus
            ) => Results.Json(await messageBus.Query<TQuery, TResponse>(query))
        );

    public static RouteHandlerBuilder MapCommand<TCommand>(
        this IEndpointRouteBuilder app, string? path = null)
        where TCommand: IRequest =>
        app.MapPost(
            path ?? $"/{typeof(TCommand).Name}",
            async (
                [FromBody] TCommand command,
                [FromServices] IMessageBus messageBus
            ) => await messageBus.Send(command)
        );
    
    public static RouteHandlerBuilder MapEvent<TEvent>(
        this IEndpointRouteBuilder app, string? path = null)
        where TEvent: INotification =>
        app.MapPost(
            path ?? $"/{typeof(TEvent).Name}",
            async (
                [FromBody] TEvent @event,
                [FromServices] IMessageBus messageBus
            ) => await messageBus.Publish(@event)
        );

}
