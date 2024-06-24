using FlowDemo.Hosting.Extensions;
using FlowDemo.Messages;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureLogging();
builder.ConfigureSerialization<CreateOrderCommand>();
builder.ConfigureNats();

builder.ConfigureMessageBus(
    c => {
        c.CommandTarget<CreateOrderCommand>("orders.commands.create");
        c.CommandTarget<TryCancelOrderCommand>("orders.commands.cancel");
        c.QueryTarget<GetOrderQuery, OrderResponse>("orders.queries.get");
        c.EventTarget<OrderCreatedEvent>("orders.events.created");
        c.EventTarget<OrderCancelledEvent>("orders.events.cancelled");
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();

app.MapGet("/now", () => DateTime.UtcNow).WithOpenApi();

app.MapCommand<CreateOrderCommand>("orders/create");
app.MapQuery<GetOrderQuery, OrderResponse>("orders/query");

app.Run();
