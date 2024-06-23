using System.Reflection;
using System.Text.Json;
using K4os.KnownTypes;
using K4os.KnownTypes.SystemTextJson;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Api.Configuration;
using K4os.NatsTransit.Api.Extensions;
using K4os.NatsTransit.Api.Handlers;
using K4os.NatsTransit.Api.Services;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.PgSql;
using MediatR;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var typesRegistry = new KnownTypesRegistry();
typesRegistry.RegisterAssembly<Program>();

builder.Services.AddSingleton(typesRegistry);
builder.Services.AddSingleton<JsonSerializerOptions>(
    _ => new JsonSerializerOptions {
        TypeInfoResolver = typesRegistry.CreateJsonTypeInfoResolver()
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblies(typeof(Program).Assembly));

builder.Services.AddSingleton<IJobScheduler, DbJobScheduler>();
builder.Services.AddSingleton<IDbJobStorage, PgSqlJobStorage>();
builder.Services.AddSingleton<IJobSerializer, SystemTextJsonJobSerializer>();
builder.Services.AddSingleton<IJobHandler, MessageBusJobHandler>();
builder.Services.AddSingleton<IPgSqlJobStorageConfig>(
    new PgSqlJobStorageConfig {
        ConnectionString = builder.Configuration.GetConnectionString("Xpovoc").ThrowIfNull()
    });
builder.Services.AddHostedService<JonSchedulerHost>();

builder.Services.Configure<NatsSettings>(builder.Configuration.GetSection("Nats"));

builder.Services.AddSingleton<NatsOpts>(
    p => {
        var settings = p.GetRequiredService<IOptions<NatsSettings>>().Value;
        return new NatsOpts {
            Url = settings.Url,
            Name = Assembly.GetEntryAssembly()?.FullName!,
            LoggerFactory = p.GetRequiredService<ILoggerFactory>()
        };
    });
builder.Services.AddSingleton<NatsJSOpts>();
builder.Services.AddSingleton<NatsConnection>();
builder.Services.AddSingleton<NatsJSContext>();
builder.Services.AddSingleton<INatsConnection, NatsConnection>();
builder.Services.AddSingleton<INatsJSContext, NatsJSContext>();
builder.Services.AddSingleton<INatsSerializerFactory, SystemJsonNatsSerializerFactory>();
builder.Services.AddSingleton<IExceptionSerializer, FakeExceptionSerializer>();
builder.Services.AddSingleton<IMessageDispatcher, ScopedMessageDispatcher>();

builder.Services.UseNatsMessageBus(
    c => {
        c.CommandTarget<CreateOrderCommand>("orders.commands.create");
        c.CommandTarget<CancelOrderCommand>("orders.commands.cancel");
        c.EventTarget<OrderCreatedEvent>("orders.events.created");
        c.EventTarget<OrderCancelledEvent>("orders.events.cancelled");

        c.Stream("orders", ["orders.>"]);

        c.CommandConsumer("orders", "commands", ["orders.commands.>"]);
        c.EventConsumer("orders", "events", true, ["orders.events.>"]);

        c.CommandSource<IRequest>("orders", "commands");
        c.EventSource<INotification>("orders", "events");
    });

builder.Host.UseSerilog(
    (context, configuration) => {
        var serilogTemplate =
            "[{Timestamp:HH:mm:ss.fff} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics", LogEventLevel.Fatal)
            .WriteTo.Console(outputTemplate: serilogTemplate);
    });

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/now", () => DateTime.UtcNow).WithOpenApi();

app.MapCommand<CreateOrderCommand>("orders/create");
app.MapQuery<GetOrderQuery, OrderResponse>("orders/query");

app.Run();