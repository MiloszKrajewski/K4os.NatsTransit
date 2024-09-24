using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace K4os.NatsTransit.Abstractions;

internal static class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ThrowIfNull<T>(
        this T? subject, 
        [CallerArgumentExpression(nameof(subject))] string? expression = null) =>
        subject ?? ThrowArgumentNull<T>(expression);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T ThrowArgumentNull<T>(string? expression) => 
        throw new ArgumentNullException(expression);
}
