using K4os.NatsTransit.Abstractions;
using Newtonsoft.Json;

namespace K4os.NatsTransit.NewtonsoftJson;

public class NewtonsoftJsonNatsAdapterFactory: INatsAdapterFactory
{
    private readonly JsonSerializerSettings _settings;

    public NewtonsoftJsonNatsAdapterFactory(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    public IOutboundAdapter<T> OutboundAdapter<T>() =>
        new NewtonsoftJsonOutboundAdapter<T>(_settings);

    public IInboundAdapter<T> InboundAdapter<T>() =>
        new NewtonsoftJsonInboundAdapter<T>(_settings);
}
