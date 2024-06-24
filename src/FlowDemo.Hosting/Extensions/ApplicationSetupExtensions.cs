using System.Reflection;
using System.Text.Json;
using FlowDemo.Hosting.Services;
using K4os.KnownTypes;
using K4os.KnownTypes.SystemTextJson;
using K4os.NatsTransit.Abstractions;
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
            (context, logging) => ConfigureLogging(
                logging, context.Configuration));
    }

    public static void ConfigureLogging(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog(
            (provider, logging) => ConfigureLogging(
                logging, provider.GetRequiredService<IConfiguration>()));
    }

    private static void ConfigureLogging(LoggerConfiguration logging, IConfiguration config)
    {
        var serilogTemplate =
            "[{Timestamp:HH:mm:ss.fff} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
        logging
            .ReadFrom.Configuration(config)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: serilogTemplate);
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
        this IHostApplicationBuilder webApplicationBuilder,
        Action<NatsMessageBusConfigurator> configure) =>
        ConfigureMessageBus(webApplicationBuilder.Services, configure);

    private static void ConfigureMessageBus(
        IServiceCollection services, 
        Action<NatsMessageBusConfigurator> configure)
    {
        services.AddSingleton<INatsSerializerFactory, SystemJsonNatsSerializerFactory>();
        services.AddSingleton<IExceptionSerializer, FakeExceptionSerializer>();
        services.AddSingleton<IMessageDispatcher, ScopedMessageDispatcher>();
        services.UseNatsMessageBus(configure);
    }
}
