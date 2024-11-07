using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Serialization;
using MediatR;

namespace K4os.NatsTransit.Targets;

public class RequestNatsTargetHandler<TRequest, TResponse>:
    NatsTargetHandler<TRequest, TResponse>
    where TRequest: IRequest<TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly string _activityName;
    private readonly NatsRequester<TRequest, TResponse> _requester;

    public record Config(
        string Subject,
        TimeSpan? Timeout = null,
        OutboundAdapter<TRequest>? OutboundPair = null,
        InboundAdapter<TResponse>? InboundPair = null
    ): INatsTargetConfig
    {
        public INatsTargetHandler CreateHandler(NatsToolbox toolbox) =>
            new RequestNatsTargetHandler<TRequest, TResponse>(toolbox, this);
    }

    public RequestNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        _toolbox = toolbox;
        _subject = config.Subject;
        var timeout = config.Timeout ?? NatsConstants.ResponseTimeout;
        var outboundPair = config.OutboundPair ?? toolbox.GetOutboundAdapter<TRequest>();
        var inboundPair = config.InboundPair ?? toolbox.GetInboundAdapter<TResponse>();
        _activityName = GetActivityName(config);
        _requester = NatsRequester.Create(toolbox, timeout, outboundPair, inboundPair);
    }

    private static string GetActivityName(Config config)
    {
        var subject = config.Subject;
        return $"Request({subject}).Send";
    }

    public override async Task<TResponse> Handle(CancellationToken token, TRequest request)
    {
        using var activity = _toolbox.Tracing.SendingScope(_activityName, true);
        var response = await _toolbox.Metrics.RequestScope(
            _subject, () => _requester.Request(token, activity, _subject, request));
        return response;
    }
}
