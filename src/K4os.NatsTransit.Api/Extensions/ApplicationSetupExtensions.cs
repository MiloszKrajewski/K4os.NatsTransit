using System.Reflection;
using System.Text.Json;
using K4os.KnownTypes;
using K4os.KnownTypes.SystemTextJson;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Api.Configuration;
using K4os.NatsTransit.Api.Services;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.PgSql;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Serilog;
using Serilog.Events;

namespace K4os.NatsTransit.Api.Extensions;

public static class ApplicationSetupExtensions
{
    public static void ConfigureLogging(this WebApplicationBuilder webApplicationBuilder)
    {
        webApplicationBuilder.Host.UseSerilog(
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
    }

    public static void ConfigureSerialization(this WebApplicationBuilder webApplicationBuilder)
    {
        var typesRegistry = new KnownTypesRegistry();
        typesRegistry.RegisterAssembly<Program>();

        webApplicationBuilder.Services.AddSingleton(typesRegistry);
        webApplicationBuilder.Services.AddSingleton<JsonSerializerOptions>(
            _ => new JsonSerializerOptions {
                TypeInfoResolver = typesRegistry.CreateJsonTypeInfoResolver()
            });
    }

    public static void ConfigureMediator(this WebApplicationBuilder webApplicationBuilder)
    {
        webApplicationBuilder.Services.AddMediatR(
            c => c.RegisterServicesFromAssemblies(typeof(Program).Assembly));
    }

    public static void ConfigureXpovoc(this WebApplicationBuilder webApplicationBuilder)
    {
        webApplicationBuilder.Services.AddSingleton<IJobScheduler, DbJobScheduler>();
        webApplicationBuilder.Services.AddSingleton<IDbJobStorage, PgSqlJobStorage>();
        webApplicationBuilder.Services.AddSingleton<IJobSerializer, SystemTextJsonJobSerializer>();
        webApplicationBuilder.Services.AddSingleton<IJobHandler, MessageBusJobHandler>();
        webApplicationBuilder.Services.AddSingleton<IPgSqlJobStorageConfig>(
            new PgSqlJobStorageConfig {
                ConnectionString = webApplicationBuilder.Configuration
                    .GetConnectionString("Xpovoc")
                    .ThrowIfNull()
            });
        webApplicationBuilder.Services.AddHostedService<JobSchedulerHost>();
    }
    
    public static void ConfigureNats(this WebApplicationBuilder webApplicationBuilder)
    {
        webApplicationBuilder.Services.Configure<NatsSettings>(webApplicationBuilder.Configuration.GetSection("Nats"));

        webApplicationBuilder.Services.AddSingleton<NatsOpts>(
            p => {
                var settings = p.GetRequiredService<IOptions<NatsSettings>>().Value;
                return new NatsOpts {
                    Url = settings.Url,
                    Name = Assembly.GetEntryAssembly()?.FullName!,
                    LoggerFactory = p.GetRequiredService<ILoggerFactory>()
                };
            });
        webApplicationBuilder.Services.AddSingleton<NatsJSOpts>();
        webApplicationBuilder.Services.AddSingleton<NatsConnection>();
        webApplicationBuilder.Services.AddSingleton<NatsJSContext>();
        webApplicationBuilder.Services.AddSingleton<INatsConnection, NatsConnection>();
        webApplicationBuilder.Services.AddSingleton<INatsJSContext, NatsJSContext>();
    }
    
    public static void ConfigureMessageBus(
        this WebApplicationBuilder webApplicationBuilder, 
        Action<NatsMessageBusConfigurator> configure)
    {
        webApplicationBuilder.Services.AddSingleton<INatsSerializerFactory, SystemJsonNatsSerializerFactory>();
        webApplicationBuilder.Services.AddSingleton<IExceptionSerializer, FakeExceptionSerializer>();
        webApplicationBuilder.Services.AddSingleton<IMessageDispatcher, ScopedMessageDispatcher>();
        webApplicationBuilder.Services.UseNatsMessageBus(configure);
    }
}
