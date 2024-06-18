using System.Diagnostics.CodeAnalysis;

namespace K4os.NatsTransit.Extensions;

public static class CollectionExtensions
{
    [return: NotNullIfNotNull("defaultValue")]
    public static V? TryGetValueOrDefault<K, V>(
        this IDictionary<K, V> dictionary, K key, V? defaultValue = default) =>
        dictionary.TryGetValue(key, out var value) ? value : defaultValue;
}
