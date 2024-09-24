using System.Buffers;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.Serialization;
using NATS.Client.Core;
using Newtonsoft.Json;

namespace K4os.NatsTransit.NewtonsoftJson;

public class NewtonsoftJsonCustomDeserializer<T>: ICustomDeserializer<T>
{
    private readonly JsonSerializerSettings _settings;

    public NewtonsoftJsonCustomDeserializer(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    public T Transform(string subject, NatsHeaders? headers, IMemoryOwner<byte> payload)
    {
        throw new NotImplementedException();
    }
}
