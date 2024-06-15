using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using MediatR;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

public class RequestNatsTargetHandler<TRequest, TResponse>:
    NatsTargetHandler<TRequest, TResponse>
    where TRequest: IRequest<TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly TimeSpan _timeout;
    private readonly INatsSerialize<TRequest> _serializer;
    private readonly INatsDeserialize<TResponse> _deserializer;

    public record Config(string Subject, TimeSpan? Timeout = null): INatsTargetConfig
    {
        public INatsTargetHandler CreateHandler(NatsToolbox toolbox) =>
            new RequestNatsTargetHandler<TRequest, TResponse>(toolbox, this);
    }

    public RequestNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        _subject = config.Subject;
        _timeout = config.Timeout ?? NatsConstants.ResponseTimeout;
        _toolbox = toolbox;
        _serializer = toolbox.Serializer<TRequest>();
        _deserializer = toolbox.Deserializer<TResponse>();
    }

    public override async Task<TResponse?> Handle(CancellationToken token, TRequest request)
    {
        // some context why it is done this way:
        // https://github.com/nats-io/nats.py/discussions/221
        // long story short: JS does not have request/reply semantics, only CORE (non-durable)
        var replySubject = $"$reply.{Guid.NewGuid():N}-{DateTime.UtcNow.Ticks:x16}";
        var subscription = _toolbox.SubscribeOne(token, replySubject, _timeout, _deserializer);
        var responseTask = /* no await */subscription.FirstOrDefault(token);
        await _toolbox.Request(token, _subject, request, replySubject, _serializer);
        var response = await responseTask;
        return _toolbox.Accept(response);
    }
}
