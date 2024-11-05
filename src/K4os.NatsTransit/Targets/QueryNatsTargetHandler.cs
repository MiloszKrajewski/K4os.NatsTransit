using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Serialization;
using MediatR;

namespace K4os.NatsTransit.Targets;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public class QueryNatsTargetHandler<TRequest, TResponse>:
    NatsTargetHandler<TRequest, TResponse>
    where TRequest: IRequest<TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly string _activityName;
    private readonly NatsInquirer<TRequest, TResponse> _requester;

    public record Config(
        string Subject,
        TimeSpan? Timeout = null,
        OutboundAdapter<TRequest>? RequestAdapter = null,
        InboundAdapter<TResponse>? ResponseAdapter = null
    ): INatsTargetConfig
    {
        public INatsTargetHandler CreateHandler(NatsToolbox toolbox) =>
            new QueryNatsTargetHandler<TRequest, TResponse>(toolbox, this);
    }

    public QueryNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        _toolbox = toolbox;
        _subject = config.Subject;
        var timeout = config.Timeout ?? NatsConstants.ResponseTimeout;
        var serializer = config.RequestAdapter ?? toolbox.GetOutboundAdapter<TRequest>();
        var deserializer = config.ResponseAdapter ?? toolbox.GetInboundAdapter<TResponse>();
        _activityName = GetActivityName(config);
        _requester = NatsInquirer.Create(toolbox, timeout, serializer, deserializer);
    }

    private static string GetActivityName(Config config)
    {
        var requestType = typeof(TRequest).GetFriendlyName();
        var responseType = typeof(TResponse).GetFriendlyName();
        var subject = config.Subject;
        return $"Request<{requestType},{responseType}>({subject})";
    }

    public override async Task<TResponse> Handle(CancellationToken token, TRequest request)
    {
        using var _ = _toolbox.Tracing.SendingScope(_activityName, true);
        var response = await _toolbox.Metrics.RequestScope(
            _subject, () => _requester.Query(token, _subject, request));
        return response;
    }
}
