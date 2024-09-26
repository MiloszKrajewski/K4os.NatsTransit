using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;
using K4os.NatsTransit.Targets;

namespace K4os.NatsTransit.Core;

public class NatsTargetSelector
{
    private readonly INatsTargetHandler[] _targets;
    private readonly ConcurrentDictionary<Type, INatsTargetHandler[]> _cache = new();

    public NatsTargetSelector(IEnumerable<INatsTargetHandler> targets) => 
        _targets = targets.ToArray();

    public INatsTargetHandler FindTarget(object message)
    {
        var type = message.GetType();
        var candidates = _cache.GetOrAdd(type, CreateCandidates);
        return
            candidates.FirstOrDefault(t => t.CanHandle(message)) ??
            ThrowNoTargetFound(type);
    }

    private INatsTargetHandler[] CreateCandidates(Type messageType) => (
        from handler in _targets
        let baseType = handler.BaseType
        where messageType.InheritsFrom(baseType)
        let distance = messageType.DistanceFrom(baseType)
        orderby distance
        select handler
    ).ToArray();

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static INatsTargetHandler ThrowNoTargetFound(Type type) =>
        throw new InvalidOperationException($"No target found for message {type.Name}");
}
