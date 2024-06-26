using FlowDemo.Entities;
using FlowDemo.Handlers;
using FlowDemo.Hosting.Extensions;
using FlowDemo.Messages;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureLogging();
builder.ConfigureSerialization<CreateOrderCommand>();
builder.ConfigureMediator<CreateOrderHandler>();
builder.ConfigureXpovoc();
builder.ConfigureNats();
builder.ConfigureTelemetry();

builder.Services.AddDbContext<OrdersDbContext>(options => {
    options.UseNpgsql(builder.Configuration.GetConnectionString("Storage"));
});

builder.ConfigureMessageBus(
    c => {
        c.CommandTarget<CreateOrderCommand>("orders.commands.create");
        c.CommandTarget<TryCancelOrderCommand>("orders.commands.cancel");
        c.QueryTarget<GetOrderQuery, OrderResponse>("orders.queries.get");
        c.EventTarget<OrderCreatedEvent>("orders.events.created");
        c.EventTarget<OrderCancelledEvent>("orders.events.cancelled");

        c.Stream("orders", ["orders.commands.>", "orders.events.>", "orders.requests.>"]);
        c.Consumer("orders", "commands", ["orders.commands.>"]);
        c.Consumer("orders", "events", true, ["orders.events.>"]);

        c.CommandSource<IRequest>("orders", "commands");
        c.EventSource<INotification>("orders", "events");
        c.QuerySource<GetOrderQuery, OrderResponse>("orders.queries.get");
        c.EventListener<OrderCreatedEvent>("orders.events.created");
    });

var host = builder.Build();
host.Run();
