using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Vue.Reactivity;

/// <summary>
/// A reactive <see cref="IList{T}"/> — the Vuecs counterpart of Vue 3.5's reactive-array
/// instrumentation (<c>packages/reactivity/src/arrayInstrumentations.ts</c> and the array branch of
/// <c>baseHandlers.ts</c>). Because C# cannot proxy <see cref="List{T}"/>, this is a first-class
/// implementation wrapping private storage with tracking built in.
/// <para>
/// Granularity mirrors upstream: each index has its own <see cref="Dependency"/>, plus one
/// iteration dependency (upstream <c>ARRAY_ITERATE_KEY</c>) and one length dependency (the
/// <c>length</c> key). Reading <c>list[i]</c> tracks index <c>i</c> only; enumerating,
/// <see cref="Contains"/>, and <see cref="IndexOf"/> track iteration; <see cref="Count"/> tracks
/// length. Assigning an existing index triggers that index <b>and iteration</b> but not length —
/// upstream <c>dep.ts trigger()</c> runs <c>ARRAY_ITERATE_KEY</c> for every numeric <c>SET</c>
/// while the <c>length</c> dep runs only when the length actually changes. Structural changes
/// (append, insert, remove, clear) trigger iteration, length, and the shifted indices. Indexer get
/// and set and <see cref="Count"/> are allocation-free once an index's dependency exists. Not
/// thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ReactiveList<T> : IList<T>, IReadOnlyList<T>, IReactiveTraversable
{
    private readonly List<T> _items;
    private readonly Dependency _iterate = new();
    private readonly Dependency _length = new();
    private Dictionary<int, Dependency>? _indexCells;

    /// <summary>Creates an empty reactive list.</summary>
    public ReactiveList() => _items = new List<T>();

    /// <summary>Creates a reactive list with the given initial <paramref name="capacity"/>.</summary>
    /// <param name="capacity">The initial capacity.</param>
    public ReactiveList(int capacity) => _items = new List<T>(capacity);

    /// <summary>Creates a reactive list seeded with <paramref name="items"/> (no reads are tracked).</summary>
    /// <param name="items">The initial elements.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public ReactiveList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = new List<T>(items);
    }

    /// <summary>The element count (reading it tracks the length dependency — upstream <c>length</c>).</summary>
    public int Count
    {
        get
        {
            _length.Track();
            return _items.Count;
        }
    }

    /// <summary>Always <see langword="false"/>; a reactive list is mutable.</summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the element at <paramref name="index"/>. The getter tracks that index; the setter
    /// triggers that index and iteration when the value changes per
    /// <see cref="EqualityComparer{T}.Default"/> (upstream array <c>SET</c>: sibling index deps are
    /// untouched, but <c>ARRAY_ITERATE_KEY</c> runs for every numeric change so enumerating effects
    /// observe replacements; the length dep is untouched).
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public T this[int index]
    {
        get
        {
            TrackIndex(index);
            return _items[index];
        }
        set
        {
            var existing = _items[index];
            if (!EqualityComparer<T>.Default.Equals(existing, value))
            {
                _items[index] = value;
                TriggerIndex(index);
                _iterate.Trigger();
            }
        }
    }

    /// <summary>Appends <paramref name="item"/>; triggers iteration and length (upstream array push).</summary>
    /// <param name="item">The element to append.</param>
    public void Add(T item)
    {
        _items.Add(item);
        TriggerIndex(_items.Count - 1);
        _iterate.Trigger();
        _length.Trigger();
    }

    /// <summary>Appends every element of <paramref name="items"/>, triggering iteration and length once.</summary>
    /// <param name="items">The elements to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var firstNewIndex = _items.Count;
        _items.AddRange(items);
        if (_items.Count != firstNewIndex)
        {
            TriggerIndexesFrom(firstNewIndex);
            _iterate.Trigger();
            _length.Trigger();
        }
    }

    /// <summary>
    /// Inserts <paramref name="item"/> at <paramref name="index"/>; triggers iteration, length, and
    /// every index from <paramref name="index"/> onward (their elements shifted).
    /// </summary>
    /// <param name="index">The insertion index.</param>
    /// <param name="item">The element to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        TriggerIndexesFrom(index);
        _iterate.Trigger();
        _length.Trigger();
    }

    /// <summary>
    /// Removes the element at <paramref name="index"/>; triggers iteration, length, and every index
    /// from <paramref name="index"/> onward (the tail shifted down; upstream array length shrink).
    /// </summary>
    /// <param name="index">The index to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        TriggerIndexesFrom(index);
        _iterate.Trigger();
        _length.Trigger();
    }

    /// <summary>
    /// Removes <paramref name="count"/> elements starting at <paramref name="index"/>; triggers
    /// iteration, length, and every index from <paramref name="index"/> onward.
    /// </summary>
    /// <param name="index">The start index.</param>
    /// <param name="count">The number of elements to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is invalid.</exception>
    public void RemoveRange(int index, int count)
    {
        _items.RemoveRange(index, count);
        if (count > 0)
        {
            TriggerIndexesFrom(index);
            _iterate.Trigger();
            _length.Trigger();
        }
    }

    /// <summary>
    /// Removes the first occurrence of <paramref name="item"/>. Triggers iteration, length, and the
    /// shifted indices when found. Returns whether an element was removed.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><see langword="true"/> when an element was removed.</returns>
    public bool Remove(T item)
    {
        var index = _items.IndexOf(item);
        if (index < 0)
        {
            return false;
        }
        RemoveAt(index);
        return true;
    }

    /// <summary>Removes every element; triggers iteration, length, and all tracked indices when non-empty.</summary>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }
        _items.Clear();
        TriggerAllIndexes();
        _iterate.Trigger();
        _length.Trigger();
    }

    /// <summary>
    /// Whether <paramref name="item"/> is present. Depends on iteration (the whole list), so it
    /// re-runs when the list is structurally changed — upstream <c>includes</c> instrumentation.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool Contains(T item)
    {
        _iterate.Track();
        return _items.Contains(item);
    }

    /// <summary>
    /// The index of the first occurrence of <paramref name="item"/>, or -1. Depends on iteration —
    /// upstream <c>indexOf</c> instrumentation.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>The zero-based index, or -1.</returns>
    // Parity note: upstream normalizes the needle with toRaw so a raw value finds a stored reactive
    // one. In Vuecs a reactive object is its own raw (no proxy identity to unwrap — see
    // IReactiveObject), so default equality is already raw-safe and no separate unwrap pass is needed.
    public int IndexOf(T item)
    {
        _iterate.Track();
        return _items.IndexOf(item);
    }

    /// <summary>Copies the elements into <paramref name="array"/> from <paramref name="arrayIndex"/> (tracks iteration).</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The destination start index.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        _iterate.Track();
        _items.CopyTo(array, arrayIndex);
    }

    /// <summary>Returns a struct enumerator over the elements, tracking iteration (re-runs on structural change).</summary>
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
        // Deep watch of a list depends on iteration alone — every mutation (element replacement
        // included, per the upstream numeric-SET rule) triggers it, so per-index tracking here
        // would only allocate cells without adding coverage. Recurses into element values.
        _iterate.Track();
        for (var index = 0; index < _items.Count; index++)
        {
            traversal.Visit(_items[index]);
        }
    }

    private void TrackIndex(int index)
    {
        if (!ReactivityState.CanTrack)
        {
            return;
        }
        var cells = _indexCells ??= new Dictionary<int, Dependency>();
        if (!cells.TryGetValue(index, out var dependency))
        {
            dependency = new Dependency();
            cells[index] = dependency;
        }
        dependency.Track();
    }

    private void TriggerIndex(int index)
    {
        if (_indexCells is not null && _indexCells.TryGetValue(index, out var dependency))
        {
            dependency.Trigger();
        }
    }

    private void TriggerIndexesFrom(int fromInclusive)
    {
        if (_indexCells is null)
        {
            return;
        }
        foreach (var pair in _indexCells)
        {
            if (pair.Key >= fromInclusive)
            {
                pair.Value.Trigger();
            }
        }
    }

    private void TriggerAllIndexes()
    {
        if (_indexCells is null)
        {
            return;
        }
        foreach (var dependency in _indexCells.Values)
        {
            dependency.Trigger();
        }
    }

    /// <summary>
    /// A non-allocating enumerator over a <see cref="ReactiveList{T}"/>; iteration is tracked when
    /// <see cref="GetEnumerator"/> hands it out, so consumers stay reactive without a per-iteration
    /// allocation. Structural changes during enumeration throw, mirroring <see cref="List{T}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private List<T>.Enumerator _inner;

        internal Enumerator(List<T> items) => _inner = items.GetEnumerator();

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
