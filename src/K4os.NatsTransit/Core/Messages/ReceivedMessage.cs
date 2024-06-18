using System.Buffers;
using K4os.NatsTransit.Abstractions;
using NATS.Client.Core;
using NATS.Client.JetStream;

using ReceivedBuffer = NATS.Client.Core.NatsMemoryOwner<byte>;

namespace K4os.NatsTransit.Core.Messages;

public static class ReceivedMessage
{
    internal record struct Metadata(
        string Subject,
        NatsException? Error,
        NatsHeaders? Headers,
        ReceivedBuffer? Data);
    
    internal record struct Result(
        object? Payload,
        Exception? Error);
        
    public static IReceivedMessage Create(
        NatsMsg<ReceivedBuffer> message,
        IKnownTypeResolver knownTypeResolver,
        IPayloadSerializer payloadSerializer,
        IExceptionSerializer exceptionSerializer)
    {
        var metadata = new Metadata {
            Subject = message.Subject,
            Error = message.Error,
            Headers = message.Headers,
            Data = message.Data
        };
       
        var result = Create(
            metadata,
            knownTypeResolver, 
            payloadSerializer, 
            exceptionSerializer);
        
        return new CoreNatsReceivedMessage<IMemoryOwner<byte>>(message, result.Payload, result.Error);
    }
    
    public static IReceivedMessage Create(
        NatsJSMsg<ReceivedBuffer> message,
        IKnownTypeResolver knownTypeResolver,
        IPayloadSerializer payloadSerializer,
        IExceptionSerializer exceptionSerializer)
    {
        var metadata = new Metadata {
            Subject = message.Subject,
            Error = message.Error,
            Headers = message.Headers,
            Data = message.Data
        };
       
        var result = Create(
            metadata,
            knownTypeResolver, 
            payloadSerializer, 
            exceptionSerializer);
        
        return new JetStreamReceivedMessage<ReceivedBuffer>(message, result.Payload, result.Error);
    }

    
    private static Result Create(
        Metadata metadata,
        IKnownTypeResolver knownTypeResolver, 
        IPayloadSerializer payloadSerializer,
        IExceptionSerializer exceptionSerializer)
    {
        try
        {
            if (metadata.Error is not null)
                return new Result(null, metadata.Error);

            var exception = metadata.Headers?.GetErrorHeader() switch {
                null => null, var e => exceptionSerializer.Deserialize(e)
            };
            if (exception is not null)
                return new Result(null, exception);

            var typeHint = knownTypeResolver.Resolve(
                metadata.Headers.GetKnownTypeHeader(), metadata.Subject);
            var datagram = metadata.Data?.Memory ?? Array.Empty<byte>();
            var payload = payloadSerializer.Deserialize(
                typeHint ?? typeof(object),
                new ReadOnlySequence<byte>(datagram));

            return new Result(payload, null);
        }
        catch (Exception exception)
        {
            return new Result(null, exception);
        }
        finally
        {
            metadata.Data?.Dispose();
        }
    }
}
