using FlowDemo.Hosting.Extensions;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.MessageBus;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureLogging();
builder.ConfigureSerialization<CreateOrderCommand>();
builder.ConfigureNats();
builder.ConfigureTelemetry();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.ConfigureMessageBus(
    c => {
        c.WithTopic("orders")
            .SendsCommands<CreateOrderCommand>("create-order")
            .SendsQueries<GetOrderQuery, OrderResponse>("get-order-by-id")
            .ObservesEvents(["order-created", "order-rejected"]);

        c.WithTopic("payments")
            .EmitsEvents<PaymentReceivedEvent>("payment-received");
    });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();
app.UseHealthChecks("/health");

app.MapGet("/now", () => DateTime.UtcNow).WithOpenApi();

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
            _ => Results.BadRequest("Order rejected")
        };
    }).WithOpenApi();

// app.MapCommand<CreateOrderCommand>("orders/create").WithOpenApi();
app.MapEvent<PaymentReceivedEvent>("payments/received").WithOpenApi();
app.MapQuery<GetOrderQuery, OrderResponse>("orders/query").WithOpenApi();

app.Run();
