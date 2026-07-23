using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Immutable component arguments copied from a parent render.</summary>
public sealed class ComponentArguments : IComponentArguments
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    /// <summary>Creates an empty argument set.</summary>
    public ComponentArguments()
        : this(new Dictionary<string, object?>(StringComparer.Ordinal))
    {
    }

    /// <summary>Creates an immutable snapshot of <paramref name="values"/>.</summary>
    /// <param name="values">The argument values.</param>
    public ComponentArguments(IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Dictionary<string, object?> snapshot = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> value in values)
        {
            ArgumentException.ThrowIfNullOrEmpty(value.Key);
            snapshot[value.Key] = value.Value;
        }

        _values = snapshot;
    }

    /// <inheritdoc/>
    public int Count => _values.Count;

    /// <inheritdoc/>
    public object? this[string parameterName]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(parameterName);
            _values.TryGetValue(parameterName, out object? value);
            return value;
        }
    }

    /// <inheritdoc/>
    public T? Get<T>(string parameterName)
    {
        object? value = this[parameterName];
        return value is T typed ? typed : default;
    }

    /// <inheritdoc/>
    public bool Contains(string parameterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(parameterName);
        return _values.ContainsKey(parameterName);
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

