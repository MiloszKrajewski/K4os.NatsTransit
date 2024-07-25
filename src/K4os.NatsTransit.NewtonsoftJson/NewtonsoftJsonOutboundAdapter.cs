using System.Buffers;
using K4os.NatsTransit.Abstractions;
using NATS.Client.Core;
using Newtonsoft.Json;

namespace K4os.NatsTransit.NewtonsoftJson;

public class NewtonsoftJsonOutboundAdapter<T>: IOutboundAdapter<T>
{
    private readonly JsonSerializerSettings _settings;

    public NewtonsoftJsonOutboundAdapter(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    public IBufferWriter<byte> Adapt(string subject, ref NatsHeaders? headers, T payload)
    {
        throw new NotImplementedException();
    }
}
