using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// An immutable, structurally value-equatable sequence used by the utility candidate model.
/// A defensive copy is taken at construction so editor and incremental-build cache keys cannot
/// be mutated by a caller after parsing.
/// </summary>
/// <typeparam name="T">The value-equatable element type.</typeparam>
public readonly struct UtilityCollection<T> :
    IReadOnlyList<T>,
    IEquatable<UtilityCollection<T>>
{
    private readonly T[]? items;

    /// <summary>
    /// Gets the empty collection.
    /// </summary>
    public static UtilityCollection<T> Empty { get; } = new(Array.Empty<T>());

    /// <summary>
    /// Creates an immutable collection from <paramref name="items"/>.
    /// </summary>
    /// <param name="items">The values to copy.</param>
    public UtilityCollection(IEnumerable<T> items)
    {
        this.items = items is T[] array
            ? (T[])array.Clone()
            : items.ToArray();
    }

    /// <summary>
    /// Gets the number of values.
    /// </summary>
    public int Count => items?.Length ?? 0;

    /// <summary>
    /// Gets the value at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    public T this[int index] => (items ?? Array.Empty<T>())[index];

    /// <summary>
    /// Determines whether this sequence has the same values in the same order as
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The sequence to compare.</param>
    /// <returns><see langword="true"/> when both sequences are structurally equal.</returns>
    public bool Equals(UtilityCollection<T> other)
    {
        var left = items ?? Array.Empty<T>();
        var right = other.items ?? Array.Empty<T>();
        if (left.Length != right.Length)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < left.Length; index++)
        {
            if (!comparer.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? value) =>
        value is UtilityCollection<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in items ?? Array.Empty<T>())
        {
            hash = (hash * 31) + (item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(item));
        }

        return hash;
    }

    /// <summary>
    /// Returns an enumerator over the values.
    /// </summary>
    /// <returns>An enumerator over the immutable sequence.</returns>
    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)(items ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Determines whether two collections are structurally equal.
    /// </summary>
    /// <param name="left">The left collection.</param>
    /// <param name="right">The right collection.</param>
    /// <returns><see langword="true"/> when the collections are structurally equal.</returns>
    public static bool operator ==(
        UtilityCollection<T> left,
        UtilityCollection<T> right) =>
        left.Equals(right);

    /// <summary>
    /// Determines whether two collections are structurally different.
    /// </summary>
    /// <param name="left">The left collection.</param>
    /// <param name="right">The right collection.</param>
    /// <returns><see langword="true"/> when the collections are structurally different.</returns>
    public static bool operator !=(
        UtilityCollection<T> left,
        UtilityCollection<T> right) =>
        !left.Equals(right);
}
