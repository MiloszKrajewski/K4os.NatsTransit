using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text;

namespace K4os.NatsTransit.Extensions;

public static class ExceptionExtensions
{
    [DoesNotReturn]
    public static T Rethrow<T>(this T exception)
        where T: Exception
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
        // it never actually gets returned, but allows to do some syntactic trick sometimes
        return exception;
    }

    public static string Explain(this Exception? exception)
    {
        if (exception == null) return "<null>";

        var result = new StringBuilder();
        Explain(exception, 0, result);
        return result.ToString();
    }

    private static void Explain(
        Exception? exception, int level, StringBuilder builder)
    {
        if (exception is null)
            return;

        // $"{exception.GetType().FullName}@{level}: {exception.Message}\n{exception.StackTrace}\n"
        builder
            .Append(exception.GetType().FullName).Append('@').Append(level).Append(": ")
            .AppendLine(exception.Message)
            .AppendLine(exception.StackTrace);

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
                Explain(inner, level + 1, builder);	
        }
        else
        {
            Explain(exception.InnerException, level + 1, builder);
        }
    }

    public static IEnumerable<Exception> Flatten(this Exception exception)
    {
        var exceptions = new Queue<Exception>();
        exceptions.Enqueue(exception);

        while (exceptions.Count > 0)
        {
            exception = exceptions.Dequeue();

            yield return exception;

            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    exceptions.Enqueue(inner);
            }
            else
            {
                var inner = exception.InnerException;
                if (inner is not null) exceptions.Enqueue(inner);
            }
        }
    }
}