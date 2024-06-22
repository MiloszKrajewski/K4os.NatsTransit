using K4os.NatsTransit.Extensions;

namespace K4os.NatsTransit.Abstractions;

public interface IMediatorAdapter
{
    public Task<object?> Invoke1(object message, CancellationToken token);
}

internal static class MediatorAdapterExtensions
{
    public static async Task<TResponse?> ExecuteHandler<TRequest, TResponse>(
        this IMediatorAdapter mediator, TRequest request, CancellationToken token) 
        where TRequest: notnull =>
        (TResponse?)await Task.Run(() => mediator.Invoke1(request, token), token);
    
    public static Task ExecuteHandler<TRequest>(
        this IMediatorAdapter mediator, TRequest request, CancellationToken token) 
        where TRequest: notnull =>
        Task.Run(() => mediator.Invoke1(request, token), token);

    public static async Task<Result<TResponse>> TryExecuteHandler<TRequest, TResponse>(
        this IMediatorAdapter mediator,
        TRequest request, CancellationToken token)
        where TRequest: notnull
    {
        try
        {
            // we execute as Task.Run to avoid synchronous handlers disguised as async
            var response = await ExecuteHandler<TRequest, TResponse>(mediator, request, token);
            return Result.Success(response!);
        }
        catch (Exception error)
        {
            return Result.Failure(error);
        }
    }
    
    public static Task<Result<object?>> TryExecuteHandler<TRequest>(
        this IMediatorAdapter mediator,
        TRequest request, CancellationToken token)
        where TRequest: notnull =>
        TryExecuteHandler<TRequest, object?>(mediator, request, token);
}
