using FlowDemo.Handlers;
using FlowDemo.Hosting.Extensions;
using FlowDemo.Messages;
using MediatR;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureLogging();
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

var host = builder.Build();
host.Run();
