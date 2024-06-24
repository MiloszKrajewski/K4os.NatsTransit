using System.Reflection;
using System.Text.Json;
using K4os.KnownTypes;
using K4os.KnownTypes.SystemTextJson;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Api.Services;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.PgSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Serilog;
using Serilog.Events;

namespace FlowDemo.Hosting.Extensions;

public static class ApplicationSetupExtensions
{
    public static void ConfigureLogging(
        this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog(
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

    public static void ConfigureSerialization<TAssemblyHook>(
        this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        
        var typesRegistry = new KnownTypesRegistry();
        typesRegistry.RegisterAssembly<TAssemblyHook>();

        services.AddSingleton(typesRegistry);
        services.AddSingleton<JsonSerializerOptions>(
            _ => new JsonSerializerOptions {
                TypeInfoResolver = typesRegistry.CreateJsonTypeInfoResolver()
            });
    }

    public static void ConfigureMediator<TAssemblyHook>(
        this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<TAssemblyHook>());
    }

    public static void ConfigureXpovoc(
        this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;
        services.AddSingleton<IJobScheduler, DbJobScheduler>();
        services.AddSingleton<IDbJobStorage, PgSqlJobStorage>();
        services.AddSingleton<IJobSerializer, SystemTextJsonJobSerializer>();
        services.AddSingleton<IJobHandler, MessageBusJobHandler>();
        services.AddSingleton<IPgSqlJobStorageConfig>(
            new PgSqlJobStorageConfig {
                ConnectionString = configuration.GetConnectionString("Xpovoc").ThrowIfNull()
            });
        services.AddHostedService<JobSchedulerHost>();
    }
    
    public static void ConfigureNats(
        this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddSingleton<NatsOpts>(
            p => new NatsOpts {
                Url = configuration.GetConnectionString("Nats").ThrowIfNull(),
                Name = Assembly.GetEntryAssembly()?.FullName!,
                LoggerFactory = p.GetRequiredService<ILoggerFactory>()
            });
        services.AddSingleton<NatsJSOpts>();
        services.AddSingleton<NatsConnection>();
        services.AddSingleton<NatsJSContext>();
        services.AddSingleton<INatsConnection, NatsConnection>();
        services.AddSingleton<INatsJSContext, NatsJSContext>();
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
