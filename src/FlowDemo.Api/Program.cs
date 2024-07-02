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
        c.EventTarget<PaymentReceivedEvent>("payments.events.received");
        c.QueryTarget<GetOrderQuery, OrderResponse>("orders.queries.get");

        c.EventListener<OrderCreatedEvent>("orders.events.created");
        c.EventListener<OrderRejectedEvent>("orders.events.rejected");
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
        var response = messageBus.Await(
            e =>
                e is OrderCreatedEvent oce && oce.RequestId == requestId ||
                e is OrderRejectedEvent ore && ore.RequestId == requestId);
        await messageBus.Send(command with { RequestId = requestId });
        return await response switch {
            OrderCreatedEvent oce => Results.Ok(oce),
            _ => Results.BadRequest("Order rejected"),
        };
    }).WithOpenApi();

app.MapEvent<PaymentReceivedEvent>("payments/received");

app.MapQuery<GetOrderQuery, OrderResponse>("orders/query");

app.Run();
