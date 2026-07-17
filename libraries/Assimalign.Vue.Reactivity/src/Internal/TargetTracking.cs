using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Object-keyed dependency maps — the equivalent of Vue's <c>targetMap</c>. Dependencies are stored
/// per (target, key) pair in a <see cref="ConditionalWeakTable{TKey,TValue}"/> so tracked targets
/// stay GC-collectible: when a target is collected its dependency map goes with it, while live
/// subscribers keep only the <see cref="Dependency"/> nodes they still link to. Not thread-safe
/// (JS event-loop model).
/// </summary>
internal static class TargetTracking
{
    private static readonly ConditionalWeakTable<object, Dictionary<object, Dependency>> TargetMap = new();

    /// <summary>Gets (or creates) the dependency for the given target object and key.</summary>
    internal static Dependency GetDependency(object target, object key)
    {
        var map = TargetMap.GetOrCreateValue(target);
        if (!map.TryGetValue(key, out var dependency))
        {
            dependency = new Dependency { Map = map, Key = key };
            map[key] = dependency;
        }
        return dependency;
    }

    /// <summary>Tracks a read of <paramref name="key"/> on <paramref name="target"/>.</summary>
    internal static void Track(object target, object key)
    {
        if (ReactivityState.ActiveSubscriber is not null && ReactivityState.ShouldTrack)
        {
            GetDependency(target, key).Track();
        }
    }

    /// <summary>Triggers subscribers of <paramref name="key"/> on <paramref name="target"/>, if any.</summary>
    internal static void Trigger(object target, object key)
    {
        if (!TargetMap.TryGetValue(target, out var map))
        {
            // The target has never been tracked at all — still a reactive mutation for the
            // global version fast path (upstream parity).
            ReactivityState.GlobalVersion++;
            return;
        }
        if (map.TryGetValue(key, out var dependency))
        {
            dependency.Trigger();
        }
        // Tracked target, untracked key: nothing observes it — no version bump (upstream parity).
    }
}
