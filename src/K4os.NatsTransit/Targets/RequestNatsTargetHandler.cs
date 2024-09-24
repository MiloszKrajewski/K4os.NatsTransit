using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.Serialization;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
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
        var outboundPair = config.OutboundPair ?? toolbox.Serializer<TRequest>();
        var inboundPair = config.InboundPair ?? toolbox.Deserializer<TResponse>();
        _activityName = GetActivityName(config);
        _requester = NatsRequester.Create(toolbox, timeout, outboundPair, inboundPair);
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
        using var _ = _toolbox.SendActivity(_activityName, true);
        return await _requester.Request(token, _subject, request);
    }
}
