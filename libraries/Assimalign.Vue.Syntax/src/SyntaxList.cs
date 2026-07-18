using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// An immutable list of syntax nodes with <b>structural</b> equality: two lists are equal when they hold
/// equal elements in the same order. The child/property/modifier and block/option/diagnostic collections
/// of the derived parsers' records use this so those records compare by value end to end — a plain
/// <see cref="IReadOnlyList{T}"/> or array would compare by reference and silently defeat the
/// incremental-generator cache ([V01.01.05.01]/[V01.01.06.01]). This is the public analogue of the
/// generators' internal <c>EquatableArray&lt;T&gt;</c> helper.
/// </summary>
/// <typeparam name="T">The element type, a reference type with its own value equality (a parse record).</typeparam>
public readonly struct SyntaxList<T> : IReadOnlyList<T>, IEquatable<SyntaxList<T>> where T : SyntaxNode
{
    /// <summary>The empty list.</summary>
    public static readonly SyntaxList<T> Empty = new(Array.Empty<T>());

    private readonly T[]? items;

    /// <summary>Wraps <paramref name="items"/> without copying; treat the array as owned by the list.</summary>
    /// <param name="items">The backing array.</param>
    public SyntaxList(T[] items) => this.items = items;

    /// <summary>The element count.</summary>
    public int Count => items?.Length ?? 0;

    /// <summary>The element at <paramref name="index"/>.</summary>
    /// <param name="index">The zero-based index.</param>
    public T this[int index] => items![index];

    /// <summary>Compares two lists element-by-element in order.</summary>
    /// <param name="other">The list to compare against.</param>
    public bool Equals(SyntaxList<T> other)
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
    public override bool Equals(object? obj) => obj is SyntaxList<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (items is null)
        {
            return 0;
        }

        var hash = 17;
        foreach (var item in items)
        {
            hash = (hash * 31) + (item?.GetHashCode() ?? 0);
        }

        return hash;
    }

    /// <summary>Returns an enumerator over the elements.</summary>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(items ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Structural equality operator.</summary>
    /// <param name="left">The left list.</param>
    /// <param name="right">The right list.</param>
    public static bool operator ==(SyntaxList<T> left, SyntaxList<T> right) => left.Equals(right);

    /// <summary>Structural inequality operator.</summary>
    /// <param name="left">The left list.</param>
    /// <param name="right">The right list.</param>
    public static bool operator !=(SyntaxList<T> left, SyntaxList<T> right) => !left.Equals(right);
}
