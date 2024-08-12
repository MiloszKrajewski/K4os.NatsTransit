using System.Reflection;
using System.Text.Json;
using FlowDemo.Hosting.Services;
using K4os.KnownTypes;
using K4os.KnownTypes.SystemTextJson;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.OpenTelemetry;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.PgSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace FlowDemo.Hosting.Extensions;

public static class ApplicationSetupExtensions
{
    private static readonly string ApplicationName =
        (Assembly.GetEntryAssembly()?.GetName().Name).ThrowIfNull();

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
    
    private static Uri GetTelemetryEndpoint(this IConfiguration config) =>
        new Uri(config.GetConnectionString("Otlp") ?? "http://localhost:4317");

    private static void ConfigureLogging(LoggerConfiguration logging, IConfiguration config)
    {
        var telemetryEndpoint = config.GetTelemetryEndpoint();
        var serilogTemplate =
            "[{Timestamp:HH:mm:ss.fff} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
        logging
            .ReadFrom.Configuration(config)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: serilogTemplate)
            .WriteTo.OpenTelemetry(
                x => {
                    x.Endpoint = telemetryEndpoint.ToString();
                    x.ResourceAttributes = new Dictionary<string, object> {
                        { "service.name", ApplicationName }
                    };
                    x.IncludedData =
                        IncludedData.TraceIdField |
                        IncludedData.SpanIdField |
                        IncludedData.MessageTemplateMD5HashAttribute;
                });
    }

    public static void ConfigureTelemetry(
        this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        var telemetryEndpoint = config.GetTelemetryEndpoint();

        services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(ApplicationName))
            .WithLogging()
            .WithMetrics(
                x => x
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(
                        ScopedMessageDispatcher.Meter.Name))
            .WithTracing(
                x => x
                    .SetSampler<AlwaysOnSampler>()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(
                        efc => {
                            efc.SetDbStatementForText = true;
                            efc.SetDbStatementForStoredProcedure = true;
                        })
                    .AddSource(
                        ScopedMessageDispatcher.ActivitySource.Name,
                        NatsToolbox.ActivitySource.Name
                    ));
        services.ConfigureOpenTelemetryMeterProvider(
            m => m.AddOtlpExporter(z => z.Endpoint = telemetryEndpoint));
        services.ConfigureOpenTelemetryTracerProvider(
            t => t.AddOtlpExporter(z => z.Endpoint = telemetryEndpoint));
        services.AddHealthChecks().AddCheck("default", () => HealthCheckResult.Healthy());
        services.ConfigureHttpClientDefaults(h => h.AddStandardResilienceHandler());

        services.AddMetrics();
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
        services.AddSingleton<ISchedulerConfig>(
            new SchedulerConfig { PollInterval = TimeSpan.FromSeconds(1) });
        services.AddSingleton<IDbJobStorage, PgSqlJobStorage>();
        services.AddSingleton<IJobSerializer, SystemTextJsonJobSerializer>();
        services.AddSingleton<IJobHandler, MessageBusJobHandler>();
        services.AddSingleton<IPgSqlJobStorageConfig>(
            new PgSqlJobStorageConfig {
                Schema = "xpovoc",
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
                Name = ApplicationName,
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
        Action<IFluentNats> configure) =>
        ConfigureMessageBus(webApplicationBuilder.Services, configure);

    private static void ConfigureMessageBus(
        IServiceCollection services,
        Action<IFluentNats> configure)
    {
        services.AddSingleton<INatsSerializerFactory, SystemJsonNatsSerializerFactory>();
        services.AddSingleton<IExceptionSerializer, DumbExceptionSerializer>();
        services.AddSingleton<IMessageDispatcher, ScopedMessageDispatcher>();
        services.AddSingleton<INatsMessageTracer, NatsMessageTracer>();
        services.UseNatsMessageBus(configure);
    }
}
