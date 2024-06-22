using System.Diagnostics;
using MediatR;

namespace K4os.NatsTransit.Api.Services;

public class AnyMessageMonitor { }

public class RequestMonitor<TRequest, TResponse>: 
    AnyMessageMonitor,
    IPipelineBehavior<TRequest, TResponse>
    where TRequest: notnull
{
    private static readonly string LoggerName = $"RequestMonitor<{typeof(TRequest).Name}>";

    protected readonly ILogger Log;

    public RequestMonitor(ILoggerFactory loggerFactory)
    {
        Log = loggerFactory.CreateLogger(LoggerName);
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType().Name;
        Log.LogInformation("Processing {RequestType}", requestType);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await next();
            stopwatch.Stop();
            var elapsed = stopwatch.Elapsed;
            Log.LogInformation(
                "{RequestType} processed ({Time:0.0}ms)", 
                requestType, elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.Elapsed;
            Log.LogError(
                e, "{Request} failed ({Time:0.0}ms)", 
                requestType, elapsed.TotalMilliseconds);
            throw;
        }
    }
}
