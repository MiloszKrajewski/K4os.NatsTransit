using System.Collections.Concurrent;
using System.Diagnostics;
using K4os.NatsTransit.Abstractions;
using MediatR;

namespace K4os.NatsTransit.Api.Services;

public class ScopedMessageDispatcher: IMessageDispatcher
{
    protected readonly ILogger Log;

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
        var stopwatch = Stopwatch.StartNew();
        var typeName = message.GetType().Name;
        Log.LogInformation("Message {TypeName} received", typeName);
        try
        {
            var result = await ScopedInvoke(message, typeName, token);
            var elapsed = stopwatch.Elapsed.TotalMilliseconds;
            Log.LogInformation(
                "Message {TypeName} processed ({Time:0} ms)",
                typeName, elapsed);
            return result;
        }
        catch (Exception e)
        {
            var elapsed = stopwatch.Elapsed.TotalMilliseconds;
            Log.LogError(
                e, "Processing {TypeName} has failed ({Time:0} ms)",
                typeName, elapsed);
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

    private readonly ConcurrentDictionary<Type, bool> _isQueryCache = new();

    private bool ImplementsQuery(Type type) =>
        _isQueryCache.GetOrAdd(type, static t => ImplementsQueryImpl(t));

    private static bool ImplementsQueryImpl(Type messageType) =>
        messageType.GetInterfaces().Any(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
}
