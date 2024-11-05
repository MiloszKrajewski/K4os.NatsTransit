namespace K4os.NatsTransit.Extensions;

internal static class CollectionExtensions
{
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? sequence) =>
        sequence ?? [];

    public static IEnumerable<T> SelectNotNull<T>(this IEnumerable<T?> sequence) =>
        sequence.Where(x => x is not null).Select(x => x!);
}
