using System.Reflection;
using System.Text.Json;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Api.Configuration;
using K4os.NatsTransit.Api.Extensions;
using K4os.NatsTransit.Api.Handlers;
using K4os.NatsTransit.Core;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblies(typeof(Program).Assembly));

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
builder.Services.AddSingleton<JsonSerializerOptions>(_ => new JsonSerializerOptions());
builder.Services.AddSingleton<INatsSerializerFactory, SystemJsonNatsSerializerFactory>();
builder.Services.AddSingleton<IExceptionSerializer, FakeExceptionSerializer>();

builder.Services.UseNatsMessageBus(
    c => {
        c.Stream("orders", ["orders.>"]);
        
        c.CommandConsumer("orders", null, ["orders.commands.>"]);
        c.EventConsumer("orders", null, true, ["orders.events.>"]);
        
        c.CommandTarget<CreateOrderCommand>("orders.commands.create");
        c.CommandSource<CreateOrderCommand>("orders", "");
        
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

app.MapCommand<CreateOrderCommand>("order/create");
app.MapQuery<GetOrderQuery, OrderResponse>("order/query");

app.Run();
