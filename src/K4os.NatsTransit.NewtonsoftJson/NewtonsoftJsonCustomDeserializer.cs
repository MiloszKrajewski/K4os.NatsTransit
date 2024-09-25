using System.Buffers;
using System.Text;
using K4os.KnownTypes;
using K4os.NatsTransit.Abstractions.MessageBus;
using K4os.NatsTransit.Abstractions.Serialization;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace K4os.NatsTransit.NewtonsoftJson;

public class NewtonsoftJsonCustomDeserializer<T>: ICustomDeserializer<T>
{
    private readonly JsonSerializerSettings _settings;
    private readonly KnownTypesRegistry? _registry;

    public NewtonsoftJsonCustomDeserializer(JsonSerializerSettings settings, KnownTypesRegistry? registry = null)
    {
        _settings = settings;
        _registry = registry;
    }

    public T Transform(string subject, IDictionary<string, StringValues>? headers, IMemoryOwner<byte> payload)
    {
        var type = headers?.TryGetValue(NatsConstants.KnownTypeHeaderName, out var alias) ?? false
            ? _registry?.TryGetType(alias!)
            : null;
        var span = payload.Memory.Span;
        var text = Encoding.UTF8.GetString(span);
        var data = JsonConvert.DeserializeObject(text, type ?? typeof(T), _settings);
        return (T?)data!;
    }
}
