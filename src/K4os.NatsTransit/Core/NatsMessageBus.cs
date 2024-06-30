using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Sources;
using K4os.NatsTransit.Targets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

public class NatsMessageBus: IHostedService, IMessageBus
{
    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;

    private readonly INatsContextAction[] _actions;
    private readonly INatsSourceHandler[] _sources;
    private readonly INatsTargetHandler[] _targets;
    private readonly IMessageDispatcher _dispatcher;

    private readonly TaskCompletionSource _started = new();
    private readonly CancellationTokenSource _cts;

    public NatsMessageBus(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext context,
        INatsSerializerFactory serializerFactory,
        IExceptionSerializer exceptionSerializer,
        IMessageDispatcher dispatcher,
        INatsMessageTracer? messageTracer,
        IEnumerable<INatsContextAction> actions,
        IEnumerable<INatsTargetConfig> targets,
        IEnumerable<INatsSourceConfig> sources)
    {
        Log = loggerFactory.CreateLogger<NatsMessageBus>();
        var toolbox = _toolbox = new NatsToolbox(
            loggerFactory,
            connection, context,
            serializerFactory, exceptionSerializer, 
            messageTracer);
        _dispatcher = dispatcher;
        _actions = actions.ToArray();
        _sources = sources.Select(s => s.CreateHandler(toolbox)).ToArray();
        _targets = targets.Select(s => s.CreateHandler(toolbox)).ToArray();
        _cts = new CancellationTokenSource();
    }

    public void WaitForStartup()
    {
        if (_started.Task.IsCompletedSuccessfully)
            return;

        throw new TimeoutException("Service bus has not been started yet");
    }

    public Task<object?> Dispatch(object message, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));
        WaitForStartup();
        return FindTarget(message).Handle(token, message);
    }

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    public async Task<object?> Await(
        Func<object, bool> predicate,
        TimeSpan? timeout = null,
        CancellationToken token = default)
    {
        WaitForStartup();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token, _cts.Token);
        var tcs = new TaskCompletionSource<object?>();
        var delay = Task.Delay(timeout ?? NatsConstants.ResponseTimeout, cts.Token);
        var subscription = _toolbox.Events.Subscribe(new EventCapture(predicate, tcs));
        try
        {
            var task = tcs.Task;
            var done = await Task.WhenAny(delay, task);
            if (done == delay) throw new TimeoutException("Waiting for event timed out");
            return await task;
        }
        finally
        {
            subscription.Dispose();
            cts.Cancel();
        }
    }

    private INatsTargetHandler FindTarget(object command)
    {
        var commandType = command.GetType();
        var candidates = _targets
            .Where(t => t.CanHandleType(commandType))
            .ToArray();
        return candidates.FirstOrDefault(t => t.CanHandle(null, command)) ??
            throw new InvalidOperationException($"No target found for message {commandType.Name}");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = _cts.Token;

            foreach (var action in _actions)
            {
                await action.Configure(_toolbox);
            }

            foreach (var source in _sources)
            {
                _ = source.Subscribe(token, _dispatcher);
            }

            _started.TrySetResult();
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to start service bus");
            _started.TrySetException(error);
            throw;
        }
        
        Log.LogInformation("MessageBus service started");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }
}