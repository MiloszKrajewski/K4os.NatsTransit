using System.Buffers;
using K4os.NatsTransit.Abstractions;
using NATS.Client.Core;
using Newtonsoft.Json;

namespace K4os.NatsTransit.NewtonsoftJson;

public class NewtonsoftJsonInboundAdapter<T>: IInboundAdapter<T>
{
    private readonly JsonSerializerSettings _settings;

    public NewtonsoftJsonInboundAdapter(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    public T Adapt(string subject, NatsHeaders? headers, IMemoryOwner<byte> payload)
    {
        throw new NotImplementedException();
    }
}
