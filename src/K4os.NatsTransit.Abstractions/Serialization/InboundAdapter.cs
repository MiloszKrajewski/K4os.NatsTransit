using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions.Serialization;

public class InboundAdapter<T>
{
    public INatsDeserialize<T>? Native { get; }
    public ICustomDeserializer<T>? Custom { get; }
        
    public InboundAdapter(INatsDeserialize<T> native) => Native = native.ThrowIfNull();
    public InboundAdapter(ICustomDeserializer<T> custom) => Custom = custom.ThrowIfNull();
}
