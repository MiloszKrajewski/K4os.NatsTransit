using System.Runtime.CompilerServices;

namespace K4os.NatsTransit.Extensions;

public static class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ThrowIfNull<T>(
        this T? subject, 
        [CallerArgumentExpression(nameof(subject))] string? expression = null) =>
        subject ?? ThrowArgumentNull<T>(expression);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T ThrowArgumentNull<T>(string? expression) => 
        throw new ArgumentNullException(expression);
    
    public static T NotLessThan<T>(this T value, T min) where T: IComparable<T> =>
        Comparer<T>.Default.Compare(value, min) < 0 ? min : value;

    public static T NotMoreThan<T>(this T value, T max) where T: IComparable<T> =>
        Comparer<T>.Default.Compare(value, max) > 0 ? max : value;
}
