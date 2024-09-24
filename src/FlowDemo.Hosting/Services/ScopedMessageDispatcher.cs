using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.MessageBus;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowDemo.Hosting.Services;

public class ScopedMessageDispatcher: IMessageDispatcher
{
    protected readonly ILogger Log;

    public static readonly ActivitySource ActivitySource = new("FlowDemo");
    public static readonly Meter Meter = new("FlowDemo");

    private static readonly Counter<long> ExecutionCounter =
        Meter.CreateCounter<long>("task_executions");

    private static readonly Counter<long> ExceptionCounter =
        Meter.CreateCounter<long>("task_failures");

    // private static readonly Counter<double> TotalDuration =
    //     Meter.CreateCounter<double>("task_duration_total", "s");

    private static readonly Histogram<double> DurationHistogram =
        Meter.CreateHistogram<double>("task_duration", "ms");

    private readonly IServiceProvider _provider;

    public ScopedMessageDispatcher(
        IServiceProvider provider,
        ILoggerFactory loggerFactory)
    {
        Log = loggerFactory.CreateLogger<ScopedMessageDispatcher>();
        _provider = provider;
    }

    public async Task<object?> Dispatch(object message, CancellationToken token)
    {
        var typeName = message.GetType().Name;
        var activityName = $"Handle<{typeName}>";
        using var activity = ActivitySource.StartActivity(activityName);

        var context = OnEnter(message);
        try
        {
            return OnResult(context, await ScopedInvoke(message, typeName, token));
        }
        catch (Exception e)
        {
            OnError(context, e);
            throw;
        }
    }

    private async Task<object?> ScopedInvoke(
        object message, string typeName, CancellationToken token)
    {
        using var scope = _provider.CreateScope();
        var provider = scope.ServiceProvider;
        var mediator = provider.GetRequiredService<IMediator>();

        switch (message)
        {
            case IRequest request:
                return await mediator.Send((object)request, token);
            case INotification notification:
                await mediator.Publish(notification, token);
                return null;
            default:
                return ImplementsQuery(message.GetType())
                    ? await mediator.Send(message, token)
                    : throw new NotSupportedException($"Unsupported message type: {typeName}");
        }
    }

    public record ExecutionContext
    {
        public string TaskName { get; init; } = "";
        public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
    }

    public ExecutionContext OnEnter<TRequest>(TRequest request)
    {
        var taskName = (request?.GetType() ?? typeof(TRequest)).Name;
        Log.LogDebug("Executing {TaskName}", taskName);
        return new ExecutionContext { TaskName = taskName };
    }

    public T OnResult<T>(ExecutionContext context, T result)
    {
        Log.LogInformation(
            "Completed {TaskName} in {Elapsed:0.000}s", 
            context.TaskName, context.Stopwatch.Elapsed.TotalSeconds);
        OnExit(context, null);
        return result;
    }

    public void OnError(ExecutionContext context, Exception error)
    {
        Log.LogError(
            error, "Failed to execute {TaskName} after {Elapsed:0.000}s", 
            context.TaskName, context.Stopwatch.Elapsed.TotalSeconds);
        OnExit(context, error);
    }

    private static void OnExit(ExecutionContext context, Exception? error)
    {
        var requestNameLabel = new KeyValuePair<string, object?>("task_name", context.TaskName);
        var elapsed = context.Stopwatch.Elapsed;

        ExecutionCounter.Add(1, requestNameLabel);
        // TotalDuration.Add(elapsed.TotalSeconds, requestNameLabel);
        DurationHistogram.Record(elapsed.TotalMilliseconds, requestNameLabel);

        if (error is null) return;

        var exceptionType = error.GetType().Name;
        ExceptionCounter.Add(1, requestNameLabel, new("exception_type", exceptionType));
    }

    private readonly ConcurrentDictionary<Type, bool> _isQueryCache = new();

    private bool ImplementsQuery(Type type) =>
        _isQueryCache.GetOrAdd(type, static t => ImplementsQueryImpl(t));

    private static bool ImplementsQueryImpl(Type messageType) =>
        messageType.GetInterfaces().Any(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
}
