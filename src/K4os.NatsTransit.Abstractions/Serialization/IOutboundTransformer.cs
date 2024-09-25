using Microsoft.Extensions.Primitives;

namespace K4os.NatsTransit.Abstractions.Serialization;

public interface IOutboundTransformer<in TMessage, out TPayload>
{
    public TPayload Transform(string subject, ref Dictionary<string, StringValues>? headers, TMessage payload);
}

public class NullOutboundTransformer<TMessage>: IOutboundTransformer<TMessage, TMessage>
{
    public static NullOutboundTransformer<TMessage> Default { get; } = new();
    
    public TMessage Transform(string subject, ref Dictionary<string, StringValues>? headers, TMessage payload) => 
        payload;
}