using FlowDemo.Handlers;
using FlowDemo.Hosting.Extensions;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureLogging();
builder.ConfigureSerialization<CreateOrderCommand>();
builder.ConfigureMediator<CreateOrderHandler>();
builder.ConfigureXpovoc();
builder.ConfigureNats();

builder.ConfigureMessageBus(
    c => {
        c.Stream("orders", ["orders.>"]);
        c.Consumer("orders", "commands", ["orders.commands.>"]);
        c.Consumer("orders", "events", true, ["orders.events.>"]);

        c.CommandTarget<CreateOrderCommand>("orders.commands.create");
        c.CommandTarget<CancelOrderCommand>("orders.commands.cancel");
        c.QueryTarget<GetOrderQuery, OrderResponse>("queries.get-order-by-id");
        c.EventTarget<OrderCreatedEvent>("orders.events.created");
        c.EventTarget<OrderCancelledEvent>("orders.events.cancelled");

        c.CommandSource<IRequest>("orders", "commands");
        c.EventSource<INotification>("orders", "events");
        c.QuerySource<GetOrderQuery, OrderResponse>("queries.get-order-by-id");
        c.EventListener<OrderCreatedEvent>("orders.events.created");
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();

app.MapGet("/now", () => DateTime.UtcNow).WithOpenApi();

// app.MapCommand<CreateOrderCommand>("orders/create");

app.MapPost(
    "orders/create",
    async (
        [FromBody] CreateOrderCommand request,
        [FromServices] IMessageBus messageBus
    ) => {
        var response = messageBus.Await<OrderCreatedEvent>(
            x => x.RequestId == request.RequestId);
        await messageBus.Send(request);
        return await response;
    }).WithOpenApi();

app.MapQuery<GetOrderQuery, OrderResponse>("orders/query");

app.Run();
