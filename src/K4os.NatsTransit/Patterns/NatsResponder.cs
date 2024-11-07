using System.Diagnostics;
using K4os.NatsTransit.Core;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Serialization;
using NATS.Client.Core;

namespace K4os.NatsTransit.Patterns;

public class NatsResponder
{
    public static NatsResponder<TMessage> Create<TMessage>(
        NatsToolbox toolbox, OutboundAdapter<TMessage> outboundAdapter) =>
        new(toolbox, outboundAdapter);
}

public class NatsResponder<TMessage>
{
    private readonly NatsToolbox _toolbox;
    private readonly OutboundAdapter<TMessage> _adapter;

    public NatsResponder(NatsToolbox toolbox, OutboundAdapter<TMessage> outboundAdapter)
    {
        _toolbox = toolbox;
        _adapter = outboundAdapter;
    }

    public ValueTask Respond<TPayload>(
        CancellationToken token, Activity? activity, 
        NatsMsg<TPayload> request, Exception error)
    {
        activity?.OnSending(request.ReplyTo ?? "<unknown>", error).OnException(error);
        return _toolbox.Respond(token, request, error);
    }

    public ValueTask Respond<TPayload>(
        CancellationToken token, Activity? activity, 
        NatsMsg<TPayload> request, TMessage message)
    {
        activity?.OnSending(request.ReplyTo ?? "<unknown>", message);
        return _toolbox.Respond(token, request, message, _adapter);
    }
}
