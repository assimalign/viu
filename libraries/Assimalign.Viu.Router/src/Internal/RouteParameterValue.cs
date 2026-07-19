using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// The stored value of a single route parameter — either one string or, for a repeatable
/// parameter, an ordered array of strings. Mirrors vue-router's <c>PathParams</c> value union
/// (<c>string | string[]</c>) from <c>packages/router/src/matcher/pathParserRanker.ts</c>.
/// </summary>
/// <remarks>
/// A <see langword="readonly struct"/> holding raw strings only — never a boxed <see cref="object"/>
/// and never a boxed <see cref="int"/>. Typed reads (integers, etc.) are parsed on demand by
/// <see cref="RouteParameters"/>, so no boxing occurs on the storage path.
/// </remarks>
internal readonly struct RouteParameterValue : IEquatable<RouteParameterValue>
{
    private readonly string singleValue;
    private readonly string[]? multipleValues;

    private RouteParameterValue(string singleValue, string[]? multipleValues)
    {
        this.singleValue = singleValue;
        this.multipleValues = multipleValues;
    }

    /// <summary>Creates a single-string value.</summary>
    public static RouteParameterValue Single(string value)
        => new(value ?? string.Empty, null);

    /// <summary>Creates a repeatable (multi-string) value.</summary>
    public static RouteParameterValue Multiple(string[] values)
        => new(string.Empty, values ?? []);

    /// <summary>Whether this value holds multiple strings (a repeatable parameter).</summary>
    public bool IsMultiple => multipleValues is not null;

    /// <summary>The single string value (empty when this is a multiple value).</summary>
    public string SingleValue => singleValue;

    /// <summary>The ordered strings for a repeatable value (empty when this is a single value).</summary>
    public IReadOnlyList<string> MultipleValues => multipleValues ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <inheritdoc/>
    public bool Equals(RouteParameterValue other)
    {
        if (IsMultiple != other.IsMultiple)
        {
            return false;
        }
        if (IsMultiple)
        {
            var left = multipleValues!;
            var right = other.multipleValues!;
            if (left.Length != right.Length)
            {
                return false;
            }
            for (var index = 0; index < left.Length; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }
        return string.Equals(singleValue, other.singleValue, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is RouteParameterValue other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (!IsMultiple)
        {
            return StringComparer.Ordinal.GetHashCode(singleValue);
        }
        var hash = new HashCode();
        hash.Add(multipleValues!.Length);
        foreach (var value in multipleValues!)
        {
            hash.Add(value, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}
