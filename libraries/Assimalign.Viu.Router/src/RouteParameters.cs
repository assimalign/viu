using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.Router;

/// <summary>
/// An immutable set of resolved route parameters with typed accessors. The C# port of vue-router's
/// <c>RouteParams</c> / <c>PathParams</c> (<c>packages/router/src/matcher/pathParserRanker.ts</c>).
/// Each parameter is stored as its raw string (or, for a repeatable parameter, an ordered array of
/// strings); typed reads such as <see cref="GetInteger"/> parse on demand.
/// </summary>
/// <remarks>
/// Reads never box: values are stored as strings and parsed to <see cref="int"/> only when
/// requested, so there is no boxed <see cref="object"/> on the storage path and no reflection.
/// Instances have value equality so the navigation pipeline can compare and snapshot them cheaply.
/// </remarks>
public sealed class RouteParameters : IEquatable<RouteParameters>
{
    private readonly Dictionary<string, RouteParameterValue> values;

    internal RouteParameters(Dictionary<string, RouteParameterValue> values)
    {
        this.values = values;
    }

    /// <summary>The empty parameter set.</summary>
    public static RouteParameters Empty { get; } =
        new(new Dictionary<string, RouteParameterValue>(0, StringComparer.Ordinal));

    internal static RouteParameters FromValues(Dictionary<string, RouteParameterValue> values)
        => values.Count == 0 ? Empty : new RouteParameters(values);

    /// <summary>The number of parameters.</summary>
    public int Count => values.Count;

    /// <summary>The parameter names.</summary>
    public IReadOnlyCollection<string> Names => values.Keys;

    /// <summary>Whether a parameter with the given name is present.</summary>
    /// <param name="name">The parameter name.</param>
    public bool ContainsParameter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return values.ContainsKey(name);
    }

    /// <summary>
    /// Reads a parameter as a string. A repeatable parameter's values are joined with <c>/</c>.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <exception cref="KeyNotFoundException">No parameter with that name is present.</exception>
    public string GetString(string name)
    {
        if (TryGetString(name, out var value))
        {
            return value;
        }
        throw new KeyNotFoundException($"Route parameter \"{name}\" was not present.");
    }

    /// <summary>Attempts to read a parameter as a string.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The string value when present; otherwise the empty string.</param>
    public bool TryGetString(string name, out string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (values.TryGetValue(name, out var raw))
        {
            value = raw.IsMultiple ? string.Join("/", raw.MultipleValues) : raw.SingleValue;
            return true;
        }
        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Reads a parameter as an <see cref="int"/>, parsed with the invariant culture. Never boxes.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <exception cref="KeyNotFoundException">No parameter with that name is present.</exception>
    /// <exception cref="FormatException">The parameter value is not a valid integer.</exception>
    public int GetInteger(string name)
    {
        var text = GetString(name);
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        throw new FormatException($"Route parameter \"{name}\" value \"{text}\" is not a valid integer.");
    }

    /// <summary>Attempts to read a parameter as an <see cref="int"/> (invariant culture).</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parsed integer when present and valid; otherwise <c>0</c>.</param>
    public bool TryGetInteger(string name, out int value)
    {
        if (TryGetString(name, out var text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Reads a parameter as an ordered list of strings — the natural shape for a repeatable
    /// parameter (<c>:ids+</c>/<c>:ids*</c>). A single non-empty value yields a one-element list; an
    /// absent or empty value yields an empty list.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    public IReadOnlyList<string> GetStrings(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!values.TryGetValue(name, out var raw))
        {
            return Array.Empty<string>();
        }
        if (raw.IsMultiple)
        {
            return raw.MultipleValues;
        }
        return raw.SingleValue.Length == 0 ? Array.Empty<string>() : [raw.SingleValue];
    }

    /// <summary>Returns a copy with a single-valued parameter added or replaced.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The string value.</param>
    public RouteParameters With(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        var copy = new Dictionary<string, RouteParameterValue>(values, StringComparer.Ordinal)
        {
            [name] = RouteParameterValue.Single(value),
        };
        return new RouteParameters(copy);
    }

    /// <summary>Returns a copy with a repeatable (multi-valued) parameter added or replaced.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="parameterValues">The ordered string values.</param>
    public RouteParameters WithMany(string name, params string[] parameterValues)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parameterValues);
        var copy = new Dictionary<string, RouteParameterValue>(values, StringComparer.Ordinal)
        {
            [name] = RouteParameterValue.Multiple([.. parameterValues]),
        };
        return new RouteParameters(copy);
    }

    internal bool TryGetRawValue(string name, out RouteParameterValue value)
        => values.TryGetValue(name, out value);

    /// <inheritdoc/>
    public bool Equals(RouteParameters? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (values.Count != other.values.Count)
        {
            return false;
        }
        foreach (var (name, value) in values)
        {
            if (!other.values.TryGetValue(name, out var otherValue) || !value.Equals(otherValue))
            {
                return false;
            }
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => Equals(obj as RouteParameters);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Order-independent so two equal sets built in any order hash alike.
        var accumulator = 0;
        foreach (var (name, value) in values)
        {
            accumulator ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(name), value.GetHashCode());
        }
        return accumulator;
    }
}
