using System.Runtime.CompilerServices;

namespace K4os.NatsTransit.Internal;

public class DeepTrace
{
    private static Action<string>? _logger;
    private static readonly HashSet<Type> Types = [];
    
    public static void Register<T>() => Types.Add(typeof(T));
    public static void Enable(Action<string> logger) => _logger = logger;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Obsolete("This should not used in production code.")]
    public static Action<string>? Logger<T>() => 
        Types.Contains(typeof(T)) ? _logger : null;
}
