using System.Text.Json;
using K4os.KnownTypes;
using K4os.Xpovoc.Abstractions;

namespace K4os.NatsTransit.Api.Services;

public class SystemTextJsonJobSerializer: IJobSerializer
{
    private readonly JsonSerializerOptions _options;
    private readonly KnownTypesRegistry _registry;

    public SystemTextJsonJobSerializer(
        JsonSerializerOptions options,
        KnownTypesRegistry registry)
    {
        _options = options;
        _registry = registry;
    }

    public string Serialize(object job) =>
        JsonSerializer.Serialize(job, _options);

    public object Deserialize(string payload) =>
        JsonSerializer.Deserialize<object>(payload, _options) switch {
            JsonElement { ValueKind: JsonValueKind.Object } e => TryExtractFromJsonElement(e) ?? e,
            var p => p!
        };

    private object? TryExtractFromJsonElement(JsonElement element)
    {
        if (!element.TryGetProperty("$type", out var typeAliasProperty))
            return null;

        var typeAlias = typeAliasProperty.GetString();
        if (typeAlias is null)
            return null;

        var knownType = _registry.TryGetType(typeAlias);
        if (knownType is null)
            return null;

        return element.Deserialize(knownType, _options);
    }
}
