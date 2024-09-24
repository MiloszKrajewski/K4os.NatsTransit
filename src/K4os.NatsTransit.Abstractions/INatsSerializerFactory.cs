using NATS.Client.Core;

namespace K4os.NatsTransit.Abstractions;

public interface INatsSerializers
{
    INatsSerialize<T>? PayloadSerializer<T>();
    INatsDeserialize<T>? PayloadDeserializer<T>();
    IInboundAdapter<T>? InboundAdapter<T>();
    IOutboundAdapter<T>? OutboundAdapter<T>();
    IExceptionSerializer? ExceptionSerializer();
}

public static class NatsSerializerFactoryExtensions
{
    public static OutboundPair<T> CreateSerializer<T>(this INatsSerializers factory) =>
        factory.PayloadSerializer<T>() switch {
            { } s => new OutboundPair<T>(s),
            _ => new OutboundPair<T>(factory.OutboundAdapter<T>().ThrowIfNull())
        };
    
    public static InboundPair<T> CreateDeserializer<T>(this INatsSerializers factory) =>
        factory.PayloadDeserializer<T>() switch {
            { } d => new InboundPair<T>(d),
            _ => new InboundPair<T>(factory.InboundAdapter<T>().ThrowIfNull())
        };
}

public class OutboundPair<T>
{
    public bool IsNative => Native is not null;

    public INatsSerialize<T>? Native { get; }
    public IOutboundAdapter<T>? Adapter { get; }
        
    public OutboundPair(INatsSerialize<T>? serializer) => Native = serializer;
    public OutboundPair(IOutboundAdapter<T>? adapter) => Adapter = adapter;
}

public class InboundPair<T>
{
    public bool IsNative => Native is not null;
    
    public INatsDeserialize<T>? Native { get; }
    public IInboundAdapter<T>? Adapter { get; }
        
    public InboundPair(INatsDeserialize<T>? deserializer) => Native = deserializer;
    public InboundPair(IInboundAdapter<T>? adapter) => Adapter = adapter;
}