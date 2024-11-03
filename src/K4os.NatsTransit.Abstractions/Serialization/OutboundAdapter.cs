using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions.Serialization;

public class OutboundAdapter<T>
{
    public INatsSerialize<T>? Native { get; }
    public ICustomSerializer<T>? Custom { get; }
        
    public OutboundAdapter(INatsSerialize<T> native) => Native = native.ThrowIfNull();
    public OutboundAdapter(ICustomSerializer<T> custom) => Custom = custom.ThrowIfNull();
}
