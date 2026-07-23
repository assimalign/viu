using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A lazily populated map of per-key <see cref="Dependency"/> cells shared by the reactive
/// collections — the C# analogue of one entry's dependency bucket in Vue 3.5's <c>targetMap</c>
/// (<c>packages/reactivity/src/dep.ts</c>). Reading a key tracks its cell; mutating a key triggers
/// only that cell, giving the per-key granularity that keeps an effect reading one entry from
/// re-running when a different entry changes. Cells are created on first tracked read (the one-time
/// link establishment) and reused thereafter, so steady-state reads and writes allocate nothing.
/// A <see langword="null"/> key (a valid <see cref="ReactiveSet{T}"/> member) is tracked in a
/// dedicated cell because <see cref="Dictionary{TKey,TValue}"/> rejects null keys. Not thread-safe
/// (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="TKey">The key type (dictionary key or set member).</typeparam>
// CS8714: TKey is intentionally unconstrained so a ReactiveSet<T> with a nullable member type can use
// this table. A null key is never inserted into the dictionary — it is routed to _nullCell — so the
// notnull violation cannot occur at run time.
#pragma warning disable CS8714
internal sealed class KeyedDependencyTable<TKey>
{
    private Dictionary<TKey, Dependency>? _cells;
    private Dependency? _nullCell;

    /// <summary>Tracks a read of <paramref name="key"/>, creating its cell on the first tracked read.</summary>
    internal void Track(TKey key)
    {
        if (!ReactivityState.CanTrack)
        {
            return;
        }
        if (key is null)
        {
            (_nullCell ??= new Dependency()).Track();
            return;
        }
        var cells = _cells ??= new Dictionary<TKey, Dependency>();
        if (!cells.TryGetValue(key, out var dependency))
        {
            dependency = new Dependency();
            cells[key] = dependency;
        }
        dependency.Track();
    }

    /// <summary>Triggers the subscribers of <paramref name="key"/>, if any have been established.</summary>
    internal void Trigger(TKey key)
    {
        if (key is null)
        {
            _nullCell?.Trigger();
            return;
        }
        if (_cells is not null && _cells.TryGetValue(key, out var dependency))
        {
            dependency.Trigger();
        }
    }

    /// <summary>Triggers every established key cell — the port of the ITERATE-plus-key fan-out on <c>clear</c>.</summary>
    internal void TriggerAll()
    {
        _nullCell?.Trigger();
        if (_cells is null)
        {
            return;
        }
        foreach (var dependency in _cells.Values)
        {
            dependency.Trigger();
        }
    }

    /// <summary>The cell backing <paramref name="key"/>, or <see langword="null"/> when none is tracked.</summary>
    internal Dependency? Find(TKey key)
    {
        if (key is null)
        {
            return _nullCell;
        }
        return _cells is not null && _cells.TryGetValue(key, out var dependency) ? dependency : null;
    }
}
#pragma warning restore CS8714

