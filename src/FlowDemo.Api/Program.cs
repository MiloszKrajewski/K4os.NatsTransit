using FlowDemo.Hosting.Extensions;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureLogging();
builder.ConfigureSerialization<CreateOrderCommand>();
builder.ConfigureNats();
builder.ConfigureTelemetry();

builder.ConfigureMessageBus(
    c => {
        c.CommandTarget<CreateOrderCommand>("orders.commands.create");
        c.CommandTarget<TryCancelOrderCommand>("orders.commands.cancel");
        c.QueryTarget<GetOrderQuery, OrderResponse>("orders.queries.get");
        c.EventTarget<OrderCreatedEvent>("orders.events.created");
        c.EventTarget<OrderCancelledEvent>("orders.events.cancelled");
        
        c.EventListener<OrderCreatedEvent>("orders.events.created");
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();
app.UseHealthChecks("/health");

app.MapGet("/now", () => DateTime.UtcNow).WithOpenApi();

// app.MapCommand<CreateOrderCommand>("orders/create");

app.MapPost(
    "/orders/create",
    async (
        CreateOrderCommand command,
        [FromServices] IMessageBus messageBus
    ) => {
        var requestId = Guid.NewGuid();
        var response = messageBus.Await<OrderCreatedEvent>(e => e.RequestId == requestId);
        await messageBus.Send(command with { RequestId = requestId });
        return await response;
    }).WithOpenApi();

app.MapQuery<GetOrderQuery, OrderResponse>("orders/query");

app.Run();
