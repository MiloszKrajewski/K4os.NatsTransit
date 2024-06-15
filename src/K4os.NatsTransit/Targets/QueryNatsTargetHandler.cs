using K4os.NatsTransit.Core;
using MediatR;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

public class QueryNatsTargetHandler<TRequest, TResponse>:
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
            new QueryNatsTargetHandler<TRequest, TResponse>(toolbox, this);
    }

    public QueryNatsTargetHandler(NatsToolbox toolbox, Config config)
    {
        _subject = config.Subject;
        _timeout = config.Timeout ?? NatsConstants.ResponseTimeout;
        _toolbox = toolbox;
        _serializer = toolbox.Serializer<TRequest>();
        _deserializer = toolbox.Deserializer<TResponse>();
    }

    // https://github.com/nats-io/nats.py/discussions/221

    public override async Task<TResponse?> Handle(CancellationToken token, TRequest request)
    {
        var response = await _toolbox.Query(
            token, _subject, request, _serializer, _deserializer, _timeout);
        return _toolbox.Accept(response);
    }
}
