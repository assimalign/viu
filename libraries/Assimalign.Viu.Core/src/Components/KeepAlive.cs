using System;
using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu;

/// <summary>
/// The <c>&lt;KeepAlive&gt;</c> built-in — the C# port of upstream's <c>KeepAlive</c> component
/// (<c>packages/runtime-core/src/components/KeepAlive.ts</c>,
/// https://vuejs.org/guide/built-ins/keep-alive.html). Unlike <see cref="BaseTransition"/> (which only
/// stamps hooks) and unlike <c>Teleport</c> (a special vnode type), KeepAlive is a real
/// <see cref="IComponentDefinition"/> with renderer-internal reach: it wraps a single dynamic child and,
/// instead of unmounting the outgoing child when the view switches, has the renderer move its subtree
/// into a hidden storage container (<c>deactivate</c>) and move it back on return (<c>activate</c>), so
/// the child's <c>Setup</c> runs once and all internal state is preserved across switches.
/// <para>
/// The cache is keyed by the child's vnode key when present, else by the component definition reference
/// (a typed key — never a reflected type name). <c>Include</c>/<c>Exclude</c> match on the component's
/// declared <see cref="IComponentDefinition.Name"/> (a comma-separated string, a string list, or a
/// predicate — the C# analogue of upstream's string / array / RegExp); a non-matching child mounts and
/// unmounts normally. <c>Max</c> caps the cache with least-recently-used eviction: the least-recently
/// accessed cached instance is fully unmounted when the cache would overflow. Changing
/// <c>Include</c>/<c>Exclude</c> prunes newly excluded entries; unmounting the KeepAlive unmounts every
/// cached instance. <c>Activated</c>/<c>Deactivated</c> lifecycle hooks
/// (<see cref="Lifecycle.OnActivated"/>/<see cref="Lifecycle.OnDeactivated"/>) fire on the direct child
/// and, aggregated child-before-parent, on its nested descendants.
/// </para>
/// <para>
/// The renderer wires its internals (a storage container and a real-unmount operation) onto the instance
/// before <c>Setup</c> runs (<see cref="ComponentInstance.KeepAliveContext"/>). Because
/// <see cref="Instance"/> is a shared singleton, all per-mount state (the cache, the LRU key order, the
/// current and pending keys) is <b>closure</b> state created fresh per <c>Setup</c> — never instance
/// fields. Suspense unwrapping is deferred to <c>Suspense</c> ([V01.01.03.20]). Not thread-safe
/// (single-threaded JS event-loop model).
/// </para>
/// </summary>
public sealed class KeepAlive : IComponentDefinition
{
    /// <summary>The shared component instance the compiled render references via <see cref="RenderHelpers._KeepAlive"/>.</summary>
    public static readonly KeepAlive Instance = new();

    // include / exclude / max are declared props so their values flow through the instance's
    // shallow-reactive props (upstream: props: { include, exclude, max }); the include/exclude watch
    // and the render read them reactively.
    private static readonly IReadOnlyList<ComponentPropertyDefinition> KeepAliveProperties =
    [
        new ComponentPropertyDefinition("include"),
        new ComponentPropertyDefinition("exclude"),
        new ComponentPropertyDefinition("max"),
    ];

    private KeepAlive()
    {
    }

    /// <inheritdoc/>
    public string? Name => "KeepAlive";

    /// <inheritdoc/>
    public IReadOnlyList<ComponentPropertyDefinition>? Properties => KeepAliveProperties;

    /// <inheritdoc/>
    // KeepAlive owns no element of its own; it renders its cached child, so attribute fallthrough is off.
    public bool InheritAttributes => false;

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var instance = ComponentInstance.Current!;
        // The renderer injects its internals (storage container + real unmount) before Setup runs
        // (upstream mountComponent: instance.ctx.renderer = internals).
        var unmountCached = (instance.KeepAliveContext
            ?? throw new InvalidOperationException(
                "KeepAlive can only be set up by the Viu renderer, which injects its internals before Setup runs."))
            .Unmount;

        // Per-mount cache state. Instance is a shared singleton, so this MUST be closure state, never
        // instance fields (upstream: cache/keys are createComponent-local). `keys` orders the cache
        // least-recently-used: First is the oldest (eviction candidate), Last the freshest.
        var cache = new Dictionary<object, VirtualNode>();
        var keys = new LinkedList<object>();
        var keyNodes = new Dictionary<object, LinkedListNode<object>>();
        VirtualNode? current = null;
        object? pendingCacheKey = null;

        void AddKey(object key) => keyNodes[key] = keys.AddLast(key);

        void TouchKey(object key)
        {
            if (keyNodes.TryGetValue(key, out var node))
            {
                keys.Remove(node);
                keys.AddLast(node);
            }
        }

        void RemoveKey(object key)
        {
            if (keyNodes.Remove(key, out var node))
            {
                keys.Remove(node);
            }
        }

        void PruneCacheEntry(object key)
        {
            // Upstream pruneCacheEntry: unmount the cached instance, unless it is the current active
            // child — which cannot be unmounted now, so only reset its shape flags so a later
            // switch-away tears it down normally.
            if (cache.TryGetValue(key, out var cached))
            {
                if (current is null || !IsSameCacheType(cached, current))
                {
                    unmountCached(cached);
                }
                else
                {
                    ResetKeepAliveFlags(current);
                }
            }
            cache.Remove(key);
            RemoveKey(key);
        }

        void PruneCache(Func<string, bool> filter)
        {
            // Upstream pruneCache: prune every entry whose component name fails the filter. Snapshot the
            // keys because PruneCacheEntry mutates the cache during iteration.
            foreach (var key in new List<object>(keyNodes.Keys))
            {
                if (cache.TryGetValue(key, out var cached)
                    && ComponentName(cached) is { } name
                    && !filter(name))
                {
                    PruneCacheEntry(key);
                }
            }
        }

        // Prune newly excluded entries when include/exclude change, after the render commits current
        // (upstream: watch(() => [props.include, props.exclude], ..., { flush: 'post' })).
        ViuWatch.Watch(
            new Func<object?>[] { () => properties["include"], () => properties["exclude"] },
            (_, _, _) =>
            {
                var include = properties["include"];
                var exclude = properties["exclude"];
                if (include is not null)
                {
                    PruneCache(name => Matches(include, name));
                }
                if (exclude is not null)
                {
                    PruneCache(name => !Matches(exclude, name));
                }
            },
            new WatchOptions { Flush = WatchFlushMode.Post });

        // Cache the current subtree once it is mounted / updated (upstream: onMounted/onUpdated
        // cacheSubtree). instance.Subtree is KeepAlive's rendered child, now carrying its host el.
        void CacheSubtree()
        {
            if (pendingCacheKey is not null && instance.Subtree is not null)
            {
                cache[pendingCacheKey] = instance.Subtree;
            }
        }

        Lifecycle.OnMounted(CacheSubtree);
        Lifecycle.OnUpdated(CacheSubtree);

        // On KeepAlive teardown, unmount every cached instance. The current active child unmounts as
        // part of KeepAlive's own subtree unmount, so only reset its flags and fire its deactivated
        // hook here (upstream: onBeforeUnmount).
        Lifecycle.OnBeforeUnmount(() =>
        {
            var currentChild = instance.Subtree;
            foreach (var cached in new List<VirtualNode>(cache.Values))
            {
                if (currentChild is not null && IsSameCacheType(cached, currentChild))
                {
                    ResetKeepAliveFlags(currentChild);
                    if (currentChild.Component is ComponentInstance childInstance
                        && childInstance.HasHooks(LifecycleHookKind.Deactivated))
                    {
                        Scheduler.QueuePostFlushCallback(
                            new SchedulerJob(() => childInstance.InvokeHooks(LifecycleHookKind.Deactivated)));
                    }
                }
                else
                {
                    unmountCached(cached);
                }
            }
        });

        VirtualNode? Render()
        {
            pendingCacheKey = null;
            var slots = context.Slots;
            if (slots is null || !slots.TryGetSlot("default", out var slot))
            {
                current = null;
                return null;
            }
            var children = slot(null);
            if (children is null || children.Length == 0)
            {
                current = null;
                return null;
            }
            var rawChild = children[0];
            if (children.Length > 1)
            {
                // KeepAlive wraps exactly one child; render them all uncached (upstream dev warning).
                RuntimeWarnings.Warn("KeepAlive should contain exactly one component child.");
                current = null;
                return VirtualNodeFactory.Fragment(children);
            }
            // Only a stateful component can be kept alive (upstream: else current = null; return rawVNode).
            if (rawChild is null
                || (rawChild.ShapeFlag & ShapeFlags.StatefulComponent) == 0
                || rawChild.ComponentType is not IComponentDefinition componentDefinition)
            {
                current = rawChild;
                return rawChild;
            }

            var name = componentDefinition.Name;
            var include = properties["include"];
            var exclude = properties["exclude"];
            var maxValue = properties["max"];
            if ((include is not null && (name is null || !Matches(include, name)))
                || (exclude is not null && name is not null && Matches(exclude, name)))
            {
                // Filtered out: this child mounts and unmounts normally, uncached.
                current = rawChild;
                return rawChild;
            }

            var child = rawChild;
            var key = child.Key ?? componentDefinition;
            var hasCached = cache.TryGetValue(key, out var cached);
            // Clone a reused (already-mounted) child before mutating its el/component/shapeFlag
            // (upstream: if (vnode.el) vnode = cloneVNode(vnode)). A fresh render slot vnode has no el.
            if (child.El is not null)
            {
                child = VirtualNodeFactory.Clone(child);
            }
            pendingCacheKey = key;
            if (hasCached)
            {
                // Copy the cached mounted state so the renderer reactivates instead of remounting.
                child.El = cached!.El;
                child.Component = cached.Component;
                if (child.Transition is not null)
                {
                    BaseTransition.SetTransitionHooks(child, child.Transition);
                }
                child.ShapeFlag |= ShapeFlags.ComponentKeptAlive;
                TouchKey(key);
            }
            else
            {
                AddKey(key);
                var max = ParseMax(maxValue);
                if (max > 0 && keys.Count > max)
                {
                    // Evict the least-recently-used entry (upstream: keys.values().next().value).
                    PruneCacheEntry(keys.First!.Value);
                }
            }
            // Prevent the renderer from unmounting this child: it deactivates instead.
            child.ShapeFlag |= ShapeFlags.ComponentShouldKeepAlive;
            current = child;
            return child;
        }

        return Render;
    }

    // --- name matching / helpers (upstream KeepAlive.ts module functions) ------------------------

    /// <summary>
    /// Whether <paramref name="name"/> matches an include/exclude pattern (upstream: <c>matches</c>) —
    /// a comma-separated string (segment membership, no trimming, upstream parity), a string list (any
    /// segment matches), or a predicate (the C# analogue of upstream's RegExp arm).
    /// </summary>
    private static bool Matches(object? pattern, string name) => pattern switch
    {
        string text => SplitContains(text, name),
        Func<string, bool> predicate => predicate(name),
        IEnumerable<string> list => AnyMatch(list, name),
        _ => false,
    };

    private static bool SplitContains(string pattern, string name)
    {
        // Upstream: pattern.split(',').includes(name) — exact segment equality, whitespace preserved.
        foreach (var segment in pattern.Split(','))
        {
            if (string.Equals(segment, name, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool AnyMatch(IEnumerable<string> patterns, string name)
    {
        foreach (var pattern in patterns)
        {
            if (pattern is not null && SplitContains(pattern, name))
            {
                return true;
            }
        }
        return false;
    }

    private static int ParseMax(object? max) => max switch
    {
        // Upstream: parseInt(max, 10); a non-positive / unparseable value means "unbounded".
        int value => value,
        long value => (int)value,
        string text when int.TryParse(text, out var parsed) => parsed,
        _ => 0,
    };

    private static string? ComponentName(VirtualNode vnode)
        => (vnode.ComponentType as IComponentDefinition)?.Name;

    private static bool IsSameCacheType(VirtualNode left, VirtualNode right)
        => left.Type == right.Type
            && Equals(left.Key, right.Key)
            && ReferenceEquals(left.ComponentType, right.ComponentType);

    private static void ResetKeepAliveFlags(VirtualNode vnode)
        // Upstream resetShapeFlag: clear both keep-alive bits so the vnode unmounts/mounts normally.
        => vnode.ShapeFlag &= ~(ShapeFlags.ComponentShouldKeepAlive | ShapeFlags.ComponentKeptAlive);
}
