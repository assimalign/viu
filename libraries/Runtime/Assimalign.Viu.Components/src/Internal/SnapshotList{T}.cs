using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The immutable, array-backed storage behind every virtual-node collection snapshot. The array is
/// owned exclusively by the snapshot and never escapes, so an instance is safe to share: a snapshot
/// handed back into a node or plan constructor is adopted as-is instead of being copied again,
/// which is what keeps a compiled block's dynamic-children list to a single copy per render.
/// Mutating members throw <see cref="NotSupportedException"/>. Specified by <c>[CMP-2]</c>.
/// </summary>
/// <typeparam name="T">The entry type.</typeparam>
internal sealed class SnapshotList<T> : IReadOnlyList<T>, IList<T>
{
    private readonly T[] _items;

    /// <summary>Wraps <paramref name="items"/>, which the caller must not retain or mutate.</summary>
    /// <param name="items">The entries, owned by the snapshot from this point on.</param>
    /// <param name="isNullFree">Whether every entry has already been verified non-null.</param>
    internal SnapshotList(T[] items, bool isNullFree)
    {
        _items = items;
        IsNullFree = isNullFree;
    }

    /// <summary>
    /// Whether every entry has already been verified non-null, so a consumer that requires
    /// non-null entries can adopt the snapshot without scanning it again.
    /// </summary>
    internal bool IsNullFree { get; }

    /// <summary>The owned entries, exposed only so a sibling snapshot can share the same storage.</summary>
    internal T[] Items => _items;

    /// <inheritdoc/>
    public int Count => _items.Length;

    /// <inheritdoc/>
    public T this[int index] => _items[index];

    T IList<T>.this[int index]
    {
        get => _items[index];
        set => throw CreateReadOnlyException();
    }

    bool ICollection<T>.IsReadOnly => true;

    /// <inheritdoc/>
    public bool Contains(T item) => Array.IndexOf(_items, item) >= 0;

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex) =>
        Array.Copy(_items, 0, array, arrayIndex, _items.Length);

    /// <inheritdoc/>
    public int IndexOf(T item) => Array.IndexOf(_items, item);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    void ICollection<T>.Add(T item) => throw CreateReadOnlyException();

    void ICollection<T>.Clear() => throw CreateReadOnlyException();

    void IList<T>.Insert(int index, T item) => throw CreateReadOnlyException();

    bool ICollection<T>.Remove(T item) => throw CreateReadOnlyException();

    void IList<T>.RemoveAt(int index) => throw CreateReadOnlyException();

    private static NotSupportedException CreateReadOnlyException() =>
        new("The collection snapshot is read-only.");
}
