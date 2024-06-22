using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Sources;
using K4os.NatsTransit.Targets;
using MediatR;
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
    private readonly IMediatorAdapter _mediator;

    private readonly TaskCompletionSource _started = new();
    private readonly CancellationTokenSource _cts;

    public NatsMessageBus(
        ILoggerFactory loggerFactory,
        INatsConnection connection,
        INatsJSContext context,
        INatsSerializerFactory serializerFactory,
        IExceptionSerializer exceptionSerializer,
        IMediatorAdapter mediator,
        IEnumerable<INatsContextAction> actions,
        IEnumerable<INatsTargetConfig> targets,
        IEnumerable<INatsSourceConfig> sources)
    {
        Log = loggerFactory.CreateLogger<NatsMessageBus>();
        var toolbox = _toolbox = new NatsToolbox(
            loggerFactory, connection, context, serializerFactory, exceptionSerializer);
        _mediator = mediator;
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

    public async Task Send<TCommand>(
        TCommand command, CancellationToken token = default)
        where TCommand: IRequest
    {
        WaitForStartup();
        
        var target = FindMatchingTarget<CommandNatsTargetHandler<TCommand>>();
        if (target is null) throw new InvalidOperationException("No target found for message");

        await target.Handle(token, command);
    }

    public async Task Publish<TEvent>(
        TEvent @event, CancellationToken token = default)
        where TEvent: INotification
    {
        WaitForStartup();
        
        var target = FindMatchingTarget<EventNatsTargetHandler<TEvent>>();
        if (target is null) throw new InvalidOperationException("No target found for message");

        await target.Handle(token, @event);
    }

    public async Task<TResponse> Query<TQuery, TResponse>(
        TQuery query, CancellationToken token = default)
        where TQuery: IRequest<TResponse>
    {
        WaitForStartup();

        var target = FindMatchingTarget<QueryNatsTargetHandler<TQuery, TResponse>>();
        if (target is null) throw new InvalidOperationException("No target found for message");

        var response = await target.Handle(token, query);
        if (response is null) throw new InvalidOperationException("No response received");

        return (TResponse)response;
    }

    public async Task<TResponse> Request<TRequest, TResponse>(
        TRequest request, CancellationToken token = default)
        where TRequest: IRequest<TResponse>
    {
        WaitForStartup();

        var target = FindMatchingTarget<RequestNatsTargetHandler<TRequest, TResponse>>();
        if (target is null) throw new InvalidOperationException("No target found for message");

        var response = await target.Handle(token, request);
        if (response is null) throw new InvalidOperationException("No response received");

        return (TResponse)response;
    }

    public Task<TEvent> Await<TEvent>(
        Func<TEvent, bool> predicate, 
        TimeSpan? timeout = null, 
        CancellationToken token = default) 
        where TEvent: INotification
    {
        #warning implement me
        throw new NotImplementedException();
    }

    private INatsTargetHandler? FindMatchingTarget<THandler>() =>
        _targets.FirstOrDefault(t => t is THandler);

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
                _ = source.Subscribe(token, _mediator);
            }

            _started.TrySetResult();
        }
        catch (Exception error)
        {
            Log.LogError(error, "Failed to start service bus");
            _started.TrySetException(error);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }
}
