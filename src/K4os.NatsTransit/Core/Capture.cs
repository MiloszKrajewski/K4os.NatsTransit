using System;

namespace K4os.NatsTransit.Core;

public class Capture<T>: IObserver<T>, IDisposable
{
    private readonly Func<T, bool> _predicate;
    private readonly TaskCompletionSource<T?> _result = new();
    
    public Task<T?> Task => _result.Task;

    public Capture(Func<T, bool> predicate) => _predicate = predicate;

    public void OnCompleted() => 
        _result.TrySetCanceled();

    public void OnError(Exception error) => 
        _result.TrySetException(error);

    public void OnNext(T value)
    {
        try
        {
            if (!_predicate(value)) return;

            _result.TrySetResult(value);
        }
        catch (Exception error)
        {
            _result.TrySetException(error);
        }
    }

    public void Dispose() => 
        _result.TrySetCanceled();
}
