using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Extensions;

namespace K4os.NatsTransit.Core;

internal static class MediatorAdapterExtensions
{
    public static async Task<TResponse?> ForkDispatch<TRequest, TResponse>(
        this IMessageDispatcher mediator, TRequest request, CancellationToken token) 
        where TRequest: notnull =>
        (TResponse?)await Task.Run(() => mediator.Dispatch(request, token), token);
    
    public static Task ForkDispatch<TRequest>(
        this IMessageDispatcher mediator, TRequest request, CancellationToken token) 
        where TRequest: notnull =>
        Task.Run(() => mediator.Dispatch(request, token), token);

    public static async Task<Result<TResponse>> ForkDispatchWithResult<TRequest, TResponse>(
        this IMessageDispatcher mediator,
        TRequest request, CancellationToken token)
        where TRequest: notnull
    {
        try
        {
            // we execute as Task.Run to avoid synchronous handlers disguised as async
            var response = await ForkDispatch<TRequest, TResponse>(mediator, request, token);
            return Result.Success(response!);
        }
        catch (Exception error)
        {
            return Result.Failure(error);
        }
    }
}
