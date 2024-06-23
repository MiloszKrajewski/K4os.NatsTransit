using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace K4os.NatsTransit.Api.Extensions;

public static class WebApplicationExtensions
{
    public static void MapRequest<TRequest, TResponse>(
        this IEndpointRouteBuilder app, string? path = null)
        where TRequest: IRequest<TResponse>
    {
        app.MapPost(
            path ?? $"/{typeof(TRequest).Name}",
            async (
                [FromBody] TRequest request,
                [FromServices] IMessageBus messageBus
            ) => Results.Json(await messageBus.Request<TRequest, TResponse>(request))
        ).WithOpenApi();
    }
    
    public static void MapQuery<TQuery, TResponse>(
        this IEndpointRouteBuilder app, string? path = null)
        where TQuery: IRequest<TResponse>
    {
        app.MapPost(
            path ?? $"/{typeof(TQuery).Name}",
            async (
                [FromBody] TQuery query,
                [FromServices] IMessageBus messageBus
            ) => Results.Json(await messageBus.Query<TQuery, TResponse>(query))
        ).WithOpenApi();
    }
    
    public static void MapCommand<TCommand>(
        this IEndpointRouteBuilder app, string? path = null)
        where TCommand: IRequest
    {
        app.MapPost(
            path ?? $"/{typeof(TCommand).Name}",
            async (
                [FromBody] TCommand request,
                [FromServices] IMessageBus messageBus
            ) => await messageBus.Send(request)
        ).WithOpenApi();
    }


}
