using System.Text;
using K4os.KnownTypes;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace K4os.NatsTransit.NewtonsoftJson;

public class NewtonsoftJsonCustomSerializer<T>: ICustomSerializer<T>
{
    private readonly JsonSerializerSettings _settings;
    private readonly KnownTypesRegistry? _registry;

    public NewtonsoftJsonCustomSerializer(JsonSerializerSettings settings, KnownTypesRegistry? registry = null)
    {
        _settings = settings;
        _registry = registry;
    }

    public Memory<byte> Transform(string subject, ref Dictionary<string, StringValues>? headers, T payload)
    {
        var text = JsonConvert.SerializeObject(payload, typeof(object), _settings);
        var bytes = Encoding.UTF8.GetBytes(text);
        var alias = payload is not null ? _registry?.TryGetAlias(payload.GetType()) : null;
        if (alias is not null) (headers ??= new())[NatsConstants.KnownTypeHeaderName] = alias;
        return bytes;
    }
}
