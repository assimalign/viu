using System;
using System.Collections.Generic;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The property bag carried by a <see cref="VirtualNode"/> — the C# stand-in for the plain
/// <c>props</c> object on an upstream vnode (<c>packages/runtime-core/src/vnode.ts</c>).
/// Vnodes are created on every render pass, so the bag is array-backed with ordinal linear
/// scans (elements rarely carry more than a handful of properties) and pre-sizable when the
/// creator — typically the template compiler — knows the property count up front. Beyond
/// <see cref="LinearScanLimit"/> entries it migrates to a <see cref="Dictionary{TKey,TValue}"/>
/// so lookups stay bounded. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class VirtualNodeProperties
{
    /// <summary>
    /// The entry count above which the bag migrates from linear arrays to a dictionary.
    /// </summary>
    internal const int LinearScanLimit = 16;

    private const int DefaultCapacity = 4;

    private string[] _names;
    private object?[] _values;
    private int _count;
    private Dictionary<string, object?>? _overflow;

    /// <summary>Creates an empty bag with the default capacity.</summary>
    public VirtualNodeProperties()
        : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Creates an empty bag pre-sized for <paramref name="capacity"/> entries, so a creator that
    /// knows its property count (the compiler does) allocates exactly once.
    /// </summary>
    /// <param name="capacity">The expected number of properties.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public VirtualNodeProperties(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        if (capacity > LinearScanLimit)
        {
            _names = [];
            _values = [];
            _overflow = new Dictionary<string, object?>(capacity, StringComparer.Ordinal);
        }
        else
        {
            _names = capacity == 0 ? [] : new string[capacity];
            _values = capacity == 0 ? [] : new object?[capacity];
        }
    }

    /// <summary>The number of properties in the bag.</summary>
    public int Count => _overflow?.Count ?? _count;

    /// <summary>Gets the value of <paramref name="name"/>, or null when absent.</summary>
    /// <param name="name">The property name (ordinal comparison).</param>
    public object? this[string name]
    {
        get
        {
            TryGetValue(name, out var value);
            return value;
        }
    }

    /// <summary>Sets <paramref name="name"/> to <paramref name="value"/>, replacing any existing entry.</summary>
    /// <param name="name">The property name (ordinal comparison).</param>
    /// <param name="value">The property value.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_overflow is not null)
        {
            _overflow[name] = value;
            return;
        }
        for (var index = 0; index < _count; index++)
        {
            if (string.Equals(_names[index], name, StringComparison.Ordinal))
            {
                _values[index] = value;
                return;
            }
        }
        if (_count == LinearScanLimit)
        {
            MigrateToDictionary();
            _overflow![name] = value;
            return;
        }
        if (_count == _names.Length)
        {
            var grownCapacity = Math.Min(Math.Max(_names.Length * 2, DefaultCapacity), LinearScanLimit);
            Array.Resize(ref _names, grownCapacity);
            Array.Resize(ref _values, grownCapacity);
        }
        _names[_count] = name;
        _values[_count] = value;
        _count++;
    }

    /// <summary>Looks up <paramref name="name"/>.</summary>
    /// <param name="name">The property name (ordinal comparison).</param>
    /// <param name="value">The value when present; null otherwise.</param>
    /// <returns>Whether the bag contains <paramref name="name"/>.</returns>
    public bool TryGetValue(string name, out object? value)
    {
        if (_overflow is not null)
        {
            return _overflow.TryGetValue(name, out value);
        }
        for (var index = 0; index < _count; index++)
        {
            if (string.Equals(_names[index], name, StringComparison.Ordinal))
            {
                value = _values[index];
                return true;
            }
        }
        value = null;
        return false;
    }

    /// <summary>Whether the bag contains <paramref name="name"/>.</summary>
    /// <param name="name">The property name (ordinal comparison).</param>
    public bool ContainsName(string name) => TryGetValue(name, out _);

    /// <summary>Returns an allocation-free enumerator over the name/value pairs.</summary>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>Whether the bag has migrated to its dictionary fallback.</summary>
    internal bool IsDictionaryBacked => _overflow is not null;

    private void MigrateToDictionary()
    {
        var overflow = new Dictionary<string, object?>(_count * 2, StringComparer.Ordinal);
        for (var index = 0; index < _count; index++)
        {
            overflow[_names[index]] = _values[index];
        }
        _overflow = overflow;
        _names = [];
        _values = [];
        _count = 0;
    }

    /// <summary>
    /// A struct enumerator over the bag's name/value pairs; array-backed bags enumerate in
    /// insertion order.
    /// </summary>
    public struct Enumerator
    {
        private readonly VirtualNodeProperties _properties;
        private readonly bool _isDictionaryBacked;
        private Dictionary<string, object?>.Enumerator _overflowEnumerator;
        private int _index;

        internal Enumerator(VirtualNodeProperties properties)
        {
            _properties = properties;
            _isDictionaryBacked = properties._overflow is not null;
            _overflowEnumerator = properties._overflow?.GetEnumerator() ?? default;
            _index = -1;
        }

        /// <summary>The current name/value pair.</summary>
        public KeyValuePair<string, object?> Current { get; private set; }

        /// <summary>Advances to the next pair.</summary>
        /// <returns>Whether another pair is available.</returns>
        public bool MoveNext()
        {
            if (_isDictionaryBacked)
            {
                if (_overflowEnumerator.MoveNext())
                {
                    Current = _overflowEnumerator.Current;
                    return true;
                }
                return false;
            }
            var next = _index + 1;
            if (next >= _properties._count)
            {
                return false;
            }
            _index = next;
            Current = new KeyValuePair<string, object?>(_properties._names[next], _properties._values[next]);
            return true;
        }
    }
}
