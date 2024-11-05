using System.Runtime.CompilerServices;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Extensions;

namespace K4os.NatsTransit.Core;

internal static class MessageDispatcherExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<object?> Invoke(
        IMessageDispatcher dispatcher, object request, CancellationToken token) =>
        // we try to avoid synchronous handlers disguised as async, which could mess with keep-alive mechanics
        Task.Run(() => dispatcher.Dispatch(request, token), token);

    public static async Task<TResponse?> ForkDispatch<TRequest, TResponse>(
        this IMessageDispatcher dispatcher, TRequest request, CancellationToken token)
        where TRequest: notnull =>
        (TResponse?)await Invoke(dispatcher, request, token);
    
    public static Task ForkDispatch<TRequest>(
        this IMessageDispatcher dispatcher, TRequest request, CancellationToken token) 
        where TRequest: notnull =>
        Invoke(dispatcher, request, token);

    public static async Task<Result<TResponse>> ForkDispatchWithResult<TRequest, TResponse>(
        this IMessageDispatcher dispatcher,
        TRequest request, CancellationToken token)
        where TRequest: notnull
    {
        try
        {
            var response = (TResponse?)await Invoke(dispatcher, request, token);
            return Result.Success(response!);
        }
        catch (Exception error)
        {
            return Result.Failure(error);
        }
    }
}
