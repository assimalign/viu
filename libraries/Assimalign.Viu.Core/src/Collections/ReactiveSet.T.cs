using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// A reactive <see cref="ISet{T}"/> — the Viu counterpart of Vue 3.5's <c>Set</c> instrumentation
/// (<c>packages/reactivity/src/collectionHandlers.ts</c>). Because C# cannot proxy
/// <see cref="HashSet{T}"/>, this is a first-class implementation wrapping private storage with
/// tracking built in.
/// <para>
/// Granularity mirrors upstream: each member has its own membership <see cref="Dependency"/> and
/// there is one iteration dependency. <see cref="Contains"/> tracks that member; reading
/// <see cref="Count"/> or enumerating tracks iteration. <see cref="Add(T)"/> of a new member or
/// <see cref="Remove"/> of a present one triggers that member and iteration (upstream
/// <c>ADD</c>/<c>DELETE</c>); a no-op add or remove triggers nothing. <see cref="Count"/> is
/// allocation-free once a member's dependency exists. Not thread-safe (single-threaded JS event-loop
/// model).
/// </para>
/// </summary>
/// <typeparam name="T">The member type.</typeparam>
public sealed class ReactiveSet<T> : ISet<T>, IReadOnlyCollection<T>, IReactiveTraversable
{
    private readonly HashSet<T> _items;
    private readonly Dependency _iterate = new();
    private readonly KeyedDependencyTable<T> _members = new();

    /// <summary>Creates an empty reactive set.</summary>
    public ReactiveSet() => _items = new HashSet<T>();

    /// <summary>Creates a reactive set using the given member <paramref name="comparer"/>.</summary>
    /// <param name="comparer">The member comparer, or null for the default.</param>
    public ReactiveSet(IEqualityComparer<T>? comparer) => _items = new HashSet<T>(comparer);

    /// <summary>Creates a reactive set seeded with <paramref name="items"/> (no reads are tracked).</summary>
    /// <param name="items">The initial members.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public ReactiveSet(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = new HashSet<T>(items);
    }

    /// <summary>
    /// The live underlying storage, exposed to <see cref="Reactive.ToRaw{T}(ReactiveSet{T})"/> as the
    /// untracked raw view (Vue's <c>toRaw</c> on a reactive Set). Reads off it never track and writes
    /// through it never trigger — it is the same data, minus the instrumentation.
    /// </summary>
    internal HashSet<T> RawStorage => _items;

    /// <summary>The member count (reading it tracks iteration).</summary>
    public int Count
    {
        get
        {
            _iterate.Track();
            return _items.Count;
        }
    }

    /// <summary>Always <see langword="false"/>; a reactive set is mutable.</summary>
    public bool IsReadOnly => false;

    /// <summary>Adds <paramref name="item"/>; triggers the member and iteration when newly added.</summary>
    /// <param name="item">The member to add.</param>
    /// <returns><see langword="true"/> when the member was newly added.</returns>
    public bool Add(T item)
    {
        if (_items.Add(item))
        {
            _members.Trigger(item);
            _iterate.Trigger();
            return true;
        }
        return false;
    }

    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>Removes <paramref name="item"/>; triggers the member and iteration when present.</summary>
    /// <param name="item">The member to remove.</param>
    /// <returns><see langword="true"/> when a member was removed.</returns>
    public bool Remove(T item)
    {
        if (_items.Remove(item))
        {
            _members.Trigger(item);
            _iterate.Trigger();
            return true;
        }
        return false;
    }

    /// <summary>Removes every member; triggers all tracked members and iteration when non-empty.</summary>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }
        _items.Clear();
        _members.TriggerAll();
        _iterate.Trigger();
    }

    /// <summary>Whether <paramref name="item"/> is a member (tracks that member).</summary>
    /// <param name="item">The member to test.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool Contains(T item)
    {
        _members.Track(item);
        return _items.Contains(item);
    }

    /// <summary>Adds every element of <paramref name="other"/>; triggers each newly added member and iteration once.</summary>
    /// <param name="other">The members to union in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public void UnionWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        List<T>? added = null;
        foreach (var item in other)
        {
            if (_items.Add(item))
            {
                (added ??= new List<T>()).Add(item);
            }
        }
        TriggerChanged(added);
    }

    /// <summary>Removes every element of <paramref name="other"/>; triggers each removed member and iteration once.</summary>
    /// <param name="other">The members to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public void ExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        List<T>? removed = null;
        foreach (var item in other)
        {
            if (_items.Remove(item))
            {
                (removed ??= new List<T>()).Add(item);
            }
        }
        TriggerChanged(removed);
    }

    /// <summary>Keeps only members also in <paramref name="other"/>; triggers each removed member and iteration once.</summary>
    /// <param name="other">The members to intersect with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public void IntersectWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var keep = other as ICollection<T> ?? new HashSet<T>(other);
        List<T>? removed = null;
        foreach (var item in _items)
        {
            if (!keep.Contains(item))
            {
                (removed ??= new List<T>()).Add(item);
            }
        }
        if (removed is not null)
        {
            foreach (var item in removed)
            {
                _items.Remove(item);
            }
        }
        TriggerChanged(removed);
    }

    /// <summary>Toggles membership of each element of <paramref name="other"/>; triggers each toggled member and iteration once.</summary>
    /// <param name="other">The members to symmetric-difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var source = other as HashSet<T> ?? new HashSet<T>(other, _items.Comparer);
        List<T>? changed = null;
        foreach (var item in source)
        {
            if (_items.Remove(item) || _items.Add(item))
            {
                (changed ??= new List<T>()).Add(item);
            }
        }
        TriggerChanged(changed);
    }

    /// <summary>Whether the set is a subset of <paramref name="other"/> (tracks iteration).</summary>
    /// <param name="other">The other sequence.</param>
    /// <returns><see langword="true"/> when a subset.</returns>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        _iterate.Track();
        return _items.IsSubsetOf(other);
    }

    /// <summary>Whether the set is a superset of <paramref name="other"/> (tracks iteration).</summary>
    /// <param name="other">The other sequence.</param>
    /// <returns><see langword="true"/> when a superset.</returns>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        _iterate.Track();
        return _items.IsSupersetOf(other);
    }

    /// <summary>Whether the set is a proper subset of <paramref name="other"/> (tracks iteration).</summary>
    /// <param name="other">The other sequence.</param>
    /// <returns><see langword="true"/> when a proper subset.</returns>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        _iterate.Track();
        return _items.IsProperSubsetOf(other);
    }

    /// <summary>Whether the set is a proper superset of <paramref name="other"/> (tracks iteration).</summary>
    /// <param name="other">The other sequence.</param>
    /// <returns><see langword="true"/> when a proper superset.</returns>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        _iterate.Track();
        return _items.IsProperSupersetOf(other);
    }

    /// <summary>Whether the set shares any member with <paramref name="other"/> (tracks iteration).</summary>
    /// <param name="other">The other sequence.</param>
    /// <returns><see langword="true"/> when they overlap.</returns>
    public bool Overlaps(IEnumerable<T> other)
    {
        _iterate.Track();
        return _items.Overlaps(other);
    }

    /// <summary>Whether the set contains the same members as <paramref name="other"/> (tracks iteration).</summary>
    /// <param name="other">The other sequence.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool SetEquals(IEnumerable<T> other)
    {
        _iterate.Track();
        return _items.SetEquals(other);
    }

    /// <summary>Copies the members into <paramref name="array"/> from <paramref name="arrayIndex"/> (tracks iteration).</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The destination start index.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        _iterate.Track();
        _items.CopyTo(array, arrayIndex);
    }

    /// <summary>Returns a struct enumerator over the members, tracking iteration.</summary>
    /// <returns>A non-allocating enumerator.</returns>
    public Enumerator GetEnumerator()
    {
        _iterate.Track();
        return new Enumerator(_items);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        _iterate.Track();
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        _iterate.Track();
        return _items.GetEnumerator();
    }

    /// <inheritdoc />
    void IReactiveTraversable.Traverse(ReactiveTraversal traversal)
    {
        // Iteration alone covers a deep watch — a set's only mutations are add/remove/clear and
        // all of them trigger it, so per-member tracking here would only allocate cells without
        // adding coverage. Recurses into member values.
        _iterate.Track();
        foreach (var item in _items)
        {
            traversal.Visit(item);
        }
    }

    private void TriggerChanged(List<T>? changed)
    {
        if (changed is null)
        {
            return;
        }
        foreach (var item in changed)
        {
            _members.Trigger(item);
        }
        _iterate.Trigger();
    }

    /// <summary>
    /// A non-allocating enumerator over a <see cref="ReactiveSet{T}"/>; iteration is tracked when
    /// <see cref="GetEnumerator"/> hands it out. Structural changes during enumeration throw,
    /// mirroring <see cref="HashSet{T}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private HashSet<T>.Enumerator _inner;

        internal Enumerator(HashSet<T> items) => _inner = items.GetEnumerator();

        /// <inheritdoc />
        public readonly T Current => _inner.Current;

        readonly object? IEnumerator.Current => _inner.Current;

        /// <inheritdoc />
        public bool MoveNext() => _inner.MoveNext();

        /// <inheritdoc />
        public void Dispose() => _inner.Dispose();

        void IEnumerator.Reset() => ((IEnumerator)_inner).Reset();
    }
}
