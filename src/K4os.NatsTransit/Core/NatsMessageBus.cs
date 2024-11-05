using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Configuration;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace K4os.NatsTransit.Core;

public class NatsMessageBus: IHostedService, IMessageBus
{
    protected readonly ILogger Log;

    private readonly NatsToolbox _toolbox;

    private INatsContextAction[]? _actions;
    private INatsSourceHandler[]? _sources;
    private readonly NatsTargetSelector _targets;
    private readonly IMessageDispatcher _dispatcher;
    private Func<object, CancellationToken, Task<object?>>? _publisher;

    private readonly TaskCompletionSource _started = new();
    private readonly CancellationTokenSource _cts;

    public NatsMessageBus(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext jetStream,
        INatsSerializerFactory serializerFactory,
        IExceptionSerializer? exceptionSerializer,
        IMessageDispatcher dispatcher,
        INatsMessageTracer? messageTracer,
        IEnumerable<INatsContextAction> actions,
        IEnumerable<INatsTargetConfig> targets,
        IEnumerable<INatsSourceConfig> sources)
    {
        Log = loggerFactory.CreateLogger(GetType());
        var toolbox = _toolbox = new NatsToolbox(
            loggerFactory,
            connection, jetStream,
            serializerFactory,
            exceptionSerializer,
            messageTracer);
        _dispatcher = dispatcher;
        _actions = actions.ToArray();
        _sources = sources.Select(s => s.CreateHandler(toolbox)).ToArray();
        _targets = new NatsTargetSelector(targets.Select(s => s.CreateHandler(toolbox)));
        _cts = new CancellationTokenSource();
    }
    
    public Task<object?> Dispatch(object message, CancellationToken token = default) => 
        Interlocked.CompareExchange(ref _publisher, null, null) is { } publisher 
            ? publisher(message, token) 
            : WaitForStartupThenPublishMessage(message, token);

    private Task<object?> PublishMessage(object message, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));
        var target = _targets.FindTarget(message);
        return target.Handle(token, message);
    }

    private async Task<object?> WaitForStartupThenPublishMessage(object message, CancellationToken token)
    {
        await _started.Task;
        Interlocked.CompareExchange(ref _publisher, PublishMessage, null);
        return await _publisher(message, token);
    }

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    public async Task<object?> Await(
        Func<object, bool> predicate,
        TimeSpan? timeout = null,
        CancellationToken token = default)
    {
        await _started.Task;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, _cts.Token);
        using var capture = new Capture<object>(predicate);
        using var subscription = _toolbox.Events.Subscribe(capture);
        var delay = Task.Delay(timeout ?? NatsConstants.ResponseTimeout, cts.Token);
        var done = await Task.WhenAny(delay, capture.Task);
        cts.Cancel();
        if (done == delay) throw new TimeoutException("Waiting for event timed out");
        return await capture.Task;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = _cts.Token;

            foreach (var action in _actions ?? [])
            {
                await action.Configure(_toolbox);
            }

            _actions = null; // no longer needed

            foreach (var source in _sources ?? [])
            {
                _ = source.Subscribe(token, _dispatcher);
            }
            
            _sources = null; // no longer needed

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