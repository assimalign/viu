using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// Walks a reactive value graph, reading every reachable reactive member so the ambient subscriber
/// depends on all of them — the C# port of Vue 3.5's <c>traverse()</c>
/// (<c>packages/reactivity/src/watch.ts</c>). A deep <c>watch</c> runs its source through a
/// traversal so a mutation anywhere in the graph re-runs the watcher.
/// <para>
/// Recursion is reflection-free: it descends only through <see cref="ReactiveValue"/> cells and
/// <see cref="IReactiveTraversable"/> values (source-generated <c>[Reactive]</c> objects and the
/// reactive collections), which expose their members explicitly. Plain CLR objects are leaves — a
/// deliberate divergence from Vue, whose runtime enumerates every own key, documented on
/// <see cref="IReactiveTraversable"/>. Cycles are broken by reference identity and a
/// <see cref="Depth"/> ceiling bounds descent (Vue 3.5 <c>deep: number</c> parity). Not thread-safe
/// (single-threaded JS event-loop model); construct one per traversal.
/// </para>
/// </summary>
public sealed class ReactiveTraversal
{
    private readonly HashSet<object> _seen;
    private int _remainingDepth;

    /// <summary>
    /// Creates a traversal that descends at most <paramref name="depth"/> levels. Use
    /// <see cref="int.MaxValue"/> for an unbounded walk (cycles still terminate it).
    /// </summary>
    /// <param name="depth">The maximum number of levels to descend; values &lt;= 0 visit nothing.</param>
    public ReactiveTraversal(int depth)
    {
        _remainingDepth = depth;
        _seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
    }

    /// <summary>The number of descent levels still available before the ceiling stops recursion.</summary>
    public int Depth => _remainingDepth;

    /// <summary>
    /// Descends into <paramref name="value"/>, reading (and thereby tracking) every reactive member
    /// reachable within the remaining depth. Unwraps <see cref="ReactiveValue"/> cells and recurses into
    /// <see cref="IReactiveTraversable"/> values; other values are leaves. Safe to call with
    /// <see langword="null"/> and re-entrant (the generated/collection <c>Traverse</c> calls back in).
    /// </summary>
    /// <param name="value">The value to descend into.</param>
    public void Visit(object? value)
    {
        if (value is null || _remainingDepth <= 0)
        {
            return;
        }

        // markRaw exclusion (Vue traverse checks ReactiveFlags.SKIP before any branch): a marked
        // object is a leaf here — neither its own dependency nor its members are subscribed, so a
        // deep watch never re-runs for a change inside it.
        if (RawMarkers.IsMarked(value))
        {
            return;
        }

        // A ref: reading BoxedValue tracks the ref's own dependency, then we recurse one level into the
        // unwrapped value (Vue traverse: isRef -> traverse(value.value, depth - 1)).
        if (value is ReactiveValue reference)
        {
            _remainingDepth--;
            Visit(reference.BoxedValue);
            _remainingDepth++;
            return;
        }

        // A generated reactive object or a reactive collection: descend through its explicit members.
        // Reference-identity dedup breaks cycles (A.Child = B; B.Parent = A).
        if (value is IReactiveTraversable traversable)
        {
            if (!_seen.Add(traversable))
            {
                return;
            }
            _remainingDepth--;
            traversable.Traverse(this);
            _remainingDepth++;
        }

        // Any other value is a leaf: the read that produced it already established its dependency
        // (Viu cannot reflect into plain CLR objects — see IReactiveTraversable remarks).
    }
}
