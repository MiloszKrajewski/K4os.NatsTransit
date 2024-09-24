using System.Buffers;
using K4os.NatsTransit.Abstractions;
using K4os.NatsTransit.Abstractions.Serialization;
using NATS.Client.Core;
using Newtonsoft.Json;

namespace K4os.NatsTransit.NewtonsoftJson;

public class NewtonsoftJsonCustomSerializer<T>: ICustomSerializer<T>
{
    private readonly JsonSerializerSettings _settings;

    public NewtonsoftJsonCustomSerializer(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    public IBufferWriter<byte> Adapt(string subject, ref NatsHeaders? headers, T payload)
    {
        throw new NotImplementedException();
    }
}
