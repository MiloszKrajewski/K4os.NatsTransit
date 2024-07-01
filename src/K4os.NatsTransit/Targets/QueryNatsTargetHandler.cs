using System.Diagnostics.CodeAnalysis;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Core;
using MediatR;
using NATS.Client.Core;

namespace K4os.NatsTransit.Targets;

[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last")]
public class QueryNatsTargetHandler<TRequest, TResponse>:
    NatsTargetHandler<TRequest, TResponse>
    where TRequest: IRequest<TResponse>
{
    private readonly NatsToolbox _toolbox;
    private readonly string _subject;
    private readonly TimeSpan _timeout;
    private readonly INatsSerialize<TRequest> _serializer;
    private readonly INatsDeserialize<TResponse> _deserializer;
    private readonly IOutboundAdapter<TRequest>? _requestAdapter;
    private readonly IInboundAdapter<TResponse>? _responseAdapter;
    private readonly string _activityName;

    public record Config(
        string Subject,
        TimeSpan? Timeout = null,
        IOutboundAdapter<TRequest>? RequestAdapter = null,
        IInboundAdapter<TResponse>? ResponseAdapter = null
    ): INatsTargetConfig
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
        _requestAdapter = config.RequestAdapter;
        _responseAdapter = config.ResponseAdapter;
        var requestType = typeof(TRequest).Name;
        var responseType = typeof(TResponse).Name;
        _activityName = $"Request<{requestType},{responseType}>({_subject})";
    }

    // https://github.com/nats-io/nats.py/discussions/221

    public override Task<TResponse?> Handle(CancellationToken token, TRequest request) =>
        (_requestAdapter, _responseAdapter) switch {
            (null, null) => Handle(
                token, request, 
                _serializer, NullOutboundAdapter, 
                _deserializer, NullInboundAdapter),
            ({ } requestAdapter, null) => Handle(
                token, request, 
                BinarySerializer, requestAdapter, 
                _deserializer, NullInboundAdapter),
            (null, { } responseAdapter) => Handle(
                token, request, 
                _serializer, NullOutboundAdapter, 
                BinaryDeserializer, responseAdapter),
            ({ } requestAdapter, { } responseAdapter) => Handle(
                token, request, 
                BinarySerializer, requestAdapter, 
                BinaryDeserializer, responseAdapter),
        };
    
    public async Task<TResponse?> Handle<TRequestPayload, TResponsePayload>(
        CancellationToken token, TRequest request,
        INatsSerialize<TRequestPayload> serializer,
        IOutboundAdapter<TRequest, TRequestPayload> outboundAdapter,
        INatsDeserialize<TResponsePayload> deserializer,
        IInboundAdapter<TResponsePayload, TResponse> inboundAdapter)
    {
        using var _ = _toolbox.SendActivity(_activityName);
        var response = await _toolbox.Query(
            token, _subject, request, serializer, outboundAdapter, deserializer, _timeout);
        return _toolbox.Unpack(response, inboundAdapter);
    }
}
