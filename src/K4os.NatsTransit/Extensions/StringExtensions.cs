using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace K4os.NatsTransit.Extensions;

public static class StringExtensions
{
    // private const int MaxStackAlloc = 1024;
    //
    // public static int XxHash32(this string? text)
    // {
    //     if (string.IsNullOrEmpty(text)) return 0;
    //     var length = Encoding.UTF8.GetByteCount(text);
    //     var pooled = length <= MaxStackAlloc ? null : ArrayPool<byte>.Shared.Rent(length);
    //     var buffer = pooled ?? stackalloc byte[MaxStackAlloc];
    //     var actual = Encoding.UTF8.GetBytes(text, buffer);
    //     Span<byte> result = stackalloc byte[sizeof(int)];
    //     System.IO.Hashing.XxHash32.TryHash(buffer[..actual], result, out _);
    //     if (pooled is not null) ArrayPool<byte>.Shared.Return(pooled);
    //     return BitConverter.ToInt32(result);
    // }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull("text")]
    public static byte[]? ToUtf8(this string? text) =>
        text is null ? null : Encoding.UTF8.GetBytes(text);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull("blob")]
    public static string? FromUtf8(this byte[]? blob) =>
        blob is null ? null : Encoding.UTF8.GetString(blob);
}
