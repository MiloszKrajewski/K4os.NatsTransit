using FlowDemo.Backend;
using FlowDemo.Entities;
using FlowDemo.Handlers;
using FlowDemo.Hosting.Extensions;
using FlowDemo.Messages;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.MessageBus;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureLogging();
builder.ConfigureSerialization<CreateOrderCommand>();
builder.ConfigureMediator<CreateOrderHandler>();
builder.ConfigureXpovoc();
builder.ConfigureNats();
builder.ConfigureTelemetry();

builder.Services.AddDbContext<OrdersDbContext>(
    options => {
        options.UseNpgsql(builder.Configuration.GetConnectionString("Storage"));
    });

builder.ConfigureMessageBus(
    c => {
        c.WithTopic("orders")
            .SendsCommands<TryCancelOrderCommand>("try-cancel-order")
            .SendsCommands<MarkOrderAsPaidCommand>("mark-order-paid")
            .EmitsEvents<OrderCreatedEvent>("order-created")
            .EmitsEvents<OrderRejectedEvent>("order-rejected")
            .EmitsEvents<OrderCancelledEvent>("order-cancelled")
            .RespondsToQueries<GetOrderQuery, OrderResponse>("get-order-by-id")
            .ConsumesAllCommands()
            .ConsumesAllEvents();

        c.WithTopic("notifications")
            .SendsCommands<SendNotificationCommand>("send-email")
            .ConsumesAllCommands();

        c.WithTopic("payments")
            .ConsumesEvents(["payment-received"]);
    });

var host = builder.Build();

host.Services.ApplyMigrations<OrdersDbContext>();

host.Run();
