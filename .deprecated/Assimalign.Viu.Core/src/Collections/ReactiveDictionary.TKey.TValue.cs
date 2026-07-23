using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Assimalign.Viu;

/// <summary>
/// A reactive <see cref="IDictionary{TKey,TValue}"/> — the Viu counterpart of Vue 3.5's <c>Map</c>
/// instrumentation (<c>packages/reactivity/src/collectionHandlers.ts</c>). Because C# cannot proxy
/// <see cref="Dictionary{TKey,TValue}"/>, this is a first-class implementation wrapping private
/// storage with tracking built in.
/// <para>
/// Granularity mirrors upstream: each key has its own <see cref="Dependency"/>, plus one entry
/// iteration dependency (upstream <c>ITERATE_KEY</c>) and one keys-only iteration dependency
/// (upstream <c>MAP_KEY_ITERATE_KEY</c>). Reading <c>dict[key]</c>, <see cref="ContainsKey"/>, or
/// <see cref="TryGetValue"/> tracks that key; <see cref="Count"/>, <see cref="Values"/>, and
/// enumerating track entry iteration; <see cref="Keys"/> tracks keys-only iteration. Assigning an
/// <em>existing</em> key triggers that key <b>and entry iteration</b> but not keys-only iteration —
/// upstream <c>dep.ts trigger()</c> runs <c>ITERATE_KEY</c> for a Map <c>SET</c> because
/// <c>values()</c>/<c>entries()</c> observe values, while <c>keys()</c> re-runs only on
/// <c>ADD</c>/<c>DELETE</c>. Adding or removing a key triggers that key and both iteration
/// dependencies. Indexer get and <see cref="Count"/> are allocation-free once a key's dependency
/// exists. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
public sealed class ReactiveDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IReactiveTraversable
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items;
    private readonly Dependency _iterate = new();
    private readonly Dependency _keyIterate = new();
    private readonly KeyedDependencyTable<TKey> _keys = new();

    /// <summary>Creates an empty reactive dictionary.</summary>
    public ReactiveDictionary() => _items = new Dictionary<TKey, TValue>();

    /// <summary>Creates a reactive dictionary using the given key <paramref name="comparer"/>.</summary>
    /// <param name="comparer">The key comparer, or null for the default.</param>
    public ReactiveDictionary(IEqualityComparer<TKey>? comparer) => _items = new Dictionary<TKey, TValue>(comparer);

    /// <summary>Creates a reactive dictionary seeded with <paramref name="items"/> (no reads are tracked).</summary>
    /// <param name="items">The initial entries.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    public ReactiveDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = new Dictionary<TKey, TValue>();
        foreach (var pair in items)
        {
            _items.Add(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// The live underlying storage, exposed to
    /// <see cref="Reactive.ToRaw{TKey,TValue}(ReactiveDictionary{TKey,TValue})"/> as the untracked raw
    /// view (Vue's <c>toRaw</c> on a reactive Map). Reads off it never track and writes through it never
    /// trigger — it is the same data, minus the instrumentation.
    /// </summary>
    internal Dictionary<TKey, TValue> RawStorage => _items;

    /// <summary>The entry count (reading it tracks iteration).</summary>
    public int Count
    {
        get
        {
            _iterate.Track();
            return _items.Count;
        }
    }

    /// <summary>Always <see langword="false"/>; a reactive dictionary is mutable.</summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// The keys (reading the collection tracks keys-only iteration — upstream
    /// <c>MAP_KEY_ITERATE_KEY</c>, so a value replacement does not re-run a keys-only effect).
    /// </summary>
    public ICollection<TKey> Keys
    {
        get
        {
            _keyIterate.Track();
            return _items.Keys;
        }
    }

    /// <summary>The values (reading the collection tracks iteration).</summary>
    public ICollection<TValue> Values
    {
        get
        {
            _iterate.Track();
            return _items.Values;
        }
    }

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    /// <summary>
    /// Gets or sets the value for <paramref name="key"/>. The getter tracks that key. The setter
    /// triggers that key and entry iteration when it already exists and the value changes (upstream
    /// <c>SET</c> runs <c>ITERATE_KEY</c> — <c>values()</c>/<c>entries()</c> observe values), or the
    /// key and both iteration dependencies when the key is new (upstream <c>ADD</c>).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value.</returns>
    /// <exception cref="KeyNotFoundException">The getter did not find <paramref name="key"/>.</exception>
    public TValue this[TKey key]
    {
        get
        {
            _keys.Track(key);
            return _items[key];
        }
        set
        {
            if (_items.TryGetValue(key, out var existing))
            {
                if (!EqualityComparer<TValue>.Default.Equals(existing, value))
                {
                    _items[key] = value;
                    _keys.Trigger(key);
                    _iterate.Trigger();
                }
                return;
            }
            _items[key] = value;
            _keys.Trigger(key);
            _iterate.Trigger();
            _keyIterate.Trigger();
        }
    }

    /// <summary>Adds a new entry; triggers the key and both iteration dependencies (upstream <c>ADD</c>).</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> already exists.</exception>
    public void Add(TKey key, TValue value)
    {
        _items.Add(key, value);
        _keys.Trigger(key);
        _iterate.Trigger();
        _keyIterate.Trigger();
    }

    /// <summary>Removes the entry for <paramref name="key"/>; triggers the key and both iteration dependencies when present.</summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><see langword="true"/> when an entry was removed.</returns>
    public bool Remove(TKey key)
    {
        if (_items.Remove(key))
        {
            _keys.Trigger(key);
            _iterate.Trigger();
            _keyIterate.Trigger();
            return true;
        }
        return false;
    }

    /// <summary>Removes every entry; triggers all tracked keys and iteration when non-empty.</summary>
    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }
        _items.Clear();
        _keys.TriggerAll();
        _iterate.Trigger();
        _keyIterate.Trigger();
    }

    /// <summary>Whether <paramref name="key"/> is present (tracks that key).</summary>
    /// <param name="key">The key to test.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool ContainsKey(TKey key)
    {
        _keys.Track(key);
        return _items.ContainsKey(key);
    }

    /// <summary>Reads the value for <paramref name="key"/> (tracks that key).</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value when found.</param>
    /// <returns><see langword="true"/> when found.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        _keys.Track(key);
        return _items.TryGetValue(key, out value);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
    {
        _keys.Track(item.Key);
        return ((ICollection<KeyValuePair<TKey, TValue>>)_items).Contains(item);
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        if (((ICollection<KeyValuePair<TKey, TValue>>)_items).Remove(item))
        {
            _keys.Trigger(item.Key);
            _iterate.Trigger();
            _keyIterate.Trigger();
            return true;
        }
        return false;
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        _iterate.Track();
        ((ICollection<KeyValuePair<TKey, TValue>>)_items).CopyTo(array, arrayIndex);
    }

    /// <summary>Returns a struct enumerator over the entries, tracking iteration.</summary>
    /// <returns>A non-allocating enumerator.</returns>
    public Enumerator GetEnumerator()
    {
        _iterate.Track();
        return new Enumerator(_items);
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
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
        // Entry iteration alone covers a deep watch — every mutation (value replacement included,
        // per the upstream Map-SET rule) triggers it, so per-key tracking here would only allocate
        // cells without adding coverage. Recurses into entry values.
        _iterate.Track();
        foreach (var pair in _items)
        {
            traversal.Visit(pair.Value);
        }
    }

    /// <summary>
    /// A non-allocating enumerator over a <see cref="ReactiveDictionary{TKey,TValue}"/>; iteration is
    /// tracked when <see cref="GetEnumerator"/> hands it out. Structural changes during enumeration
    /// throw, mirroring <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private Dictionary<TKey, TValue>.Enumerator _inner;

        internal Enumerator(Dictionary<TKey, TValue> items) => _inner = items.GetEnumerator();

        /// <inheritdoc />
        public readonly KeyValuePair<TKey, TValue> Current => _inner.Current;

        readonly object IEnumerator.Current => _inner.Current;

        /// <inheritdoc />
        public bool MoveNext() => _inner.MoveNext();

        /// <inheritdoc />
        public void Dispose() => _inner.Dispose();

        void IEnumerator.Reset() => ((IEnumerator)_inner).Reset();
    }
}
