using MediatR;

namespace K4os.NatsTransit.Core;

public class EventCapture: IObserver<INotification>
{
    private readonly Func<object, bool> _predicate;
    private readonly TaskCompletionSource<object?> _result;

    public EventCapture(Func<object, bool> predicate, TaskCompletionSource<object?> result)
    {
        _predicate = predicate;
        _result = result;
    }

    public void OnCompleted()
    {
        _result.TrySetException(new TimeoutException(""));
    }

    public void OnError(Exception error)
    {
        _result.TrySetException(error);
    }

    public void OnNext(INotification value)
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
}
