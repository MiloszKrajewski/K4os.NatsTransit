using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using K4os.NatsTransit.Extensions;
using K4os.NatsTransit.Patterns;

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
        var candidates = GetCandidates(type);
        return FindTarget(type, candidates, message);
    }

    private INatsTargetHandler[] GetCandidates(Type type) => 
        _cache.GetOrAdd(type, static (type, targets) => CreateCandidates(type, targets), _targets);

    private static INatsTargetHandler[] CreateCandidates(Type type, INatsTargetHandler[] targets) => (
        from handler in targets
        let baseType = handler.BaseType
        where type.InheritsFrom(baseType)
        let distance = type.DistanceFrom(baseType)
        orderby distance
        select handler
    ).ToArray();
    
    private static INatsTargetHandler FindTarget(Type type, INatsTargetHandler[] candidates, object message)
    {
        // we don't use .FirstOrDefault() to make it a little bit faster
        
        foreach (var candidate in candidates)
        {
            if (candidate.CanHandle(message))
                return candidate;
        }

        return ThrowNoTargetFound(type);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static INatsTargetHandler ThrowNoTargetFound(Type type) =>
        throw new InvalidOperationException($"No target found for message {type.Name}");
}
