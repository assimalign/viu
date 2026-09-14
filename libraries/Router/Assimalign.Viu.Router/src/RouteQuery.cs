using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable route query with its original text and decoded, ordered name/value pairs.
/// String accessors use ordinal names without boxing, reflection, or per-read parsing.
/// Specified by <c>[RTR-12]</c>.
/// </summary>
/// <remarks>
/// Equality compares the complete sequence of decoded pairs, including repeated names and their
/// order; raw spelling is not part of query equality. The default value is the empty query.
/// All returned collections are cached and read-only, and builders never change an existing value.
/// Query parsing and builder encoding use UTF-8 form encoding: <c>+</c> means a space and percent
/// escapes represent bytes. Malformed escapes remain literal; invalid UTF-8 and lone UTF-16
/// surrogates become replacement characters. These immutable values support concurrent reads.
/// </remarks>
public readonly struct RouteQuery : IEquatable<RouteQuery>
{
    private static readonly ReadOnlyCollection<string> EmptyStrings = Array.AsReadOnly(Array.Empty<string>());

    private readonly string? rawText;
    private readonly KeyValuePair<string, string>[]? pairs;
    private readonly Dictionary<string, IReadOnlyList<string>>? values;
    private readonly IReadOnlyCollection<string>? names;

    private RouteQuery(string rawText, KeyValuePair<string, string>[] pairs)
    {
        this.rawText = rawText;
        this.pairs = pairs;
        if (pairs.Length == 0)
        {
            values = null;
            names = null;
            return;
        }

        var groupedValues = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var orderedNames = new List<string>();
        foreach (var pair in pairs)
        {
            if (!groupedValues.TryGetValue(pair.Key, out var strings))
            {
                strings = new List<string>();
                groupedValues.Add(pair.Key, strings);
                orderedNames.Add(pair.Key);
            }
            strings.Add(pair.Value);
        }
        values = new Dictionary<string, IReadOnlyList<string>>(groupedValues.Count, StringComparer.Ordinal);
        foreach (var pair in groupedValues)
        {
            values.Add(pair.Key, pair.Value.AsReadOnly());
        }
        names = orderedNames.AsReadOnly();
    }

    /// <summary>Gets the empty query, equal to the default value. Specified by <c>[RTR-12]</c>.</summary>
    public static RouteQuery Empty => default;

    /// <summary>
    /// Gets the query text without its leading <c>?</c>. Parsing preserves the exact input;
    /// builders produce canonical form encoding. Specified by <c>[RTR-12]</c>.
    /// </summary>
    public string RawText => rawText ?? string.Empty;

    /// <summary>Gets the number of distinct decoded, ordinal names. Specified by <c>[RTR-12]</c>.</summary>
    public int Count => values?.Count ?? 0;

    /// <summary>
    /// Gets the cached, read-only distinct decoded names in first-appearance order.
    /// Specified by <c>[RTR-12]</c>.
    /// </summary>
    public IReadOnlyCollection<string> Names => names ?? EmptyStrings;

    /// <summary>
    /// Parses query text once, splitting nonempty <c>&amp;</c> segments at their first <c>=</c>.
    /// Missing assignments mean empty values; repeated and empty names are retained.
    /// Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <param name="rawQuery">The exact query text without the location's leading <c>?</c>.</param>
    /// <returns>An immutable query retaining the input text and decoded pairs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rawQuery"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The text contains a literal <c>#</c>, which starts a location's fragment. Encode query data
    /// containing that character as <c>%23</c>.
    /// </exception>
    public static RouteQuery Parse(string rawQuery)
    {
        ArgumentNullException.ThrowIfNull(rawQuery);
        if (rawQuery.Contains('#'))
        {
            throw new ArgumentException("Raw query text cannot contain the fragment delimiter '#'.", nameof(rawQuery));
        }
        return rawQuery.Length == 0 ? Empty : new RouteQuery(rawQuery, RouteQueryEncoding.Parse(rawQuery.AsSpan()));
    }

    /// <summary>
    /// Reads the first decoded value for an ordinal name, retaining an empty value when present.
    /// Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <param name="name">The decoded query name.</param>
    /// <returns>The first value in pair order, without allocation or parsing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The query does not contain the name.</exception>
    public string GetString(string name)
    {
        if (TryGetString(name, out var value))
        {
            return value;
        }
        throw new KeyNotFoundException($"Route query name \"{name}\" was not present.");
    }

    /// <summary>
    /// Attempts to read the first decoded value for an ordinal name without allocation or parsing.
    /// Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <param name="name">The decoded query name.</param>
    /// <param name="value">The first decoded value when present; otherwise the empty string.</param>
    /// <returns>True when the name is present, including when its first value is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public bool TryGetString(string name, out string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (values is not null && values.TryGetValue(name, out var strings))
        {
            value = strings[0];
            return true;
        }
        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Gets a cached, read-only list of all values for an ordinal name in pair order, including
    /// empty values. An absent name yields an empty list. Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <param name="name">The decoded query name.</param>
    /// <returns>The immutable values, without allocation or parsing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public IReadOnlyList<string> GetStrings(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return values is not null && values.TryGetValue(name, out var strings) ? strings : EmptyStrings;
    }

    /// <summary>
    /// Returns a query replacing all values of an ordinal name with one value at its first pair
    /// position, or appending a new name. The entire raw text uses canonical form encoding.
    /// Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <param name="name">The decoded query name; an empty name is allowed.</param>
    /// <param name="value">The decoded value; an empty value is retained.</param>
    /// <returns>A new query; the source is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    public RouteQuery With(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return WithValues(name, [value]);
    }

    /// <summary>
    /// Returns a query replacing an ordinal name's values at its first pair position, or appending
    /// a new name. Empty input removes the name. The input is copied and the entire raw text uses
    /// canonical form encoding. Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <param name="name">The decoded query name; an empty name is allowed.</param>
    /// <param name="queryValues">The decoded replacement values in their desired order.</param>
    /// <returns>A new query; the source and caller's input array are unchanged.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="queryValues"/>, or an input value is null.
    /// </exception>
    public RouteQuery WithMany(string name, params string[] queryValues)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(queryValues);
        foreach (var value in queryValues)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(queryValues));
        }
        return WithValues(name, queryValues);
    }

    /// <summary>
    /// Compares decoded ordered pairs using ordinal string equality, ignoring raw spelling.
    /// Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <param name="other">The query to compare.</param>
    /// <returns>True when every decoded pair and its position are equal.</returns>
    public bool Equals(RouteQuery other)
    {
        var pairCount = pairs?.Length ?? 0;
        if (pairCount != (other.pairs?.Length ?? 0))
        {
            return false;
        }
        for (var index = 0; index < pairCount; index++)
        {
            if (!string.Equals(pairs![index].Key, other.pairs![index].Key, StringComparison.Ordinal)
                || !string.Equals(pairs[index].Value, other.pairs[index].Value, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Compares another object by decoded query value. Specified by <c>[RTR-12]</c>.</summary>
    /// <param name="value">The object to compare.</param>
    /// <returns>True when the object is an equal query.</returns>
    public override bool Equals(object? value) => value is RouteQuery other && Equals(other);

    /// <summary>
    /// Hashes the decoded ordered pairs consistently with query equality. Raw spelling is ignored.
    /// Specified by <c>[RTR-12]</c>.
    /// </summary>
    /// <returns>The hash code for this query value.</returns>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        if (pairs is not null)
        {
            foreach (var pair in pairs)
            {
                hashCode.Add(pair.Key, StringComparer.Ordinal);
                hashCode.Add(pair.Value, StringComparer.Ordinal);
            }
        }
        return hashCode.ToHashCode();
    }

    /// <summary>Compares queries by decoded ordered pairs. Specified by <c>[RTR-12]</c>.</summary>
    /// <param name="left">The first query.</param>
    /// <param name="right">The second query.</param>
    /// <returns>True when the queries have equal values.</returns>
    public static bool operator ==(RouteQuery left, RouteQuery right) => left.Equals(right);

    /// <summary>Compares queries for different decoded ordered pairs. Specified by <c>[RTR-12]</c>.</summary>
    /// <param name="left">The first query.</param>
    /// <param name="right">The second query.</param>
    /// <returns>True when the queries have different values.</returns>
    public static bool operator !=(RouteQuery left, RouteQuery right) => !left.Equals(right);

    private RouteQuery WithValues(string name, ReadOnlySpan<string> queryValues)
    {
        name = RouteQueryEncoding.NormalizeScalarValueString(name);
        var replacementPairs = new List<KeyValuePair<string, string>>();
        var inserted = false;
        if (pairs is not null)
        {
            foreach (var pair in pairs)
            {
                if (string.Equals(pair.Key, name, StringComparison.Ordinal))
                {
                    if (!inserted)
                    {
                        AppendValues(replacementPairs, name, queryValues);
                        inserted = true;
                    }
                }
                else
                {
                    replacementPairs.Add(pair);
                }
            }
        }
        if (!inserted)
        {
            AppendValues(replacementPairs, name, queryValues);
        }
        if (replacementPairs.Count == 0)
        {
            return Empty;
        }
        var copiedPairs = replacementPairs.ToArray();
        return new RouteQuery(RouteQueryEncoding.Serialize(copiedPairs), copiedPairs);
    }

    private static void AppendValues(List<KeyValuePair<string, string>> destination, string name, ReadOnlySpan<string> queryValues)
    {
        foreach (var value in queryValues)
        {
            destination.Add(new KeyValuePair<string, string>(name, RouteQueryEncoding.NormalizeScalarValueString(value)));
        }
    }
}
