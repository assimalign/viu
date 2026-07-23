using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Viu.Core.Generators;

/// <summary>
/// An immutable array with value equality, used inside the incremental generator's model records so
/// the pipeline caches correctly — <see cref="System.Collections.Immutable.ImmutableArray{T}"/> and
/// raw arrays compare by reference, which would defeat the cache. Equality compares elements in order.
/// This is the well-known incremental-generator helper (see the Roslyn source-generator cookbook).
/// </summary>
/// <typeparam name="T">The element type, itself value-equatable.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    /// <summary>The empty array.</summary>
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    private readonly T[]? _items;

    /// <summary>Wraps <paramref name="items"/> (the array is not copied; treat it as owned).</summary>
    /// <param name="items">The backing array.</param>
    public EquatableArray(T[] items) => _items = items;

    /// <summary>The element count.</summary>
    public int Count => _items?.Length ?? 0;

    /// <summary>The element at <paramref name="index"/>.</summary>
    /// <param name="index">The zero-based index.</param>
    public T this[int index] => _items![index];

    /// <inheritdoc />
    public bool Equals(EquatableArray<T> other)
    {
        var left = _items ?? Array.Empty<T>();
        var right = other._items ?? Array.Empty<T>();
        if (left.Length != right.Length)
        {
            return false;
        }
        for (var index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (_items is null)
        {
            return 0;
        }
        var hash = 17;
        foreach (var item in _items)
        {
            hash = (hash * 31) + (item?.GetHashCode() ?? 0);
        }
        return hash;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
