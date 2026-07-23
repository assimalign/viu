using System;
using System.Collections;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>An immutable, ordered component attribute collection.</summary>
public sealed class ComponentAttributes : IComponentAttributeCollection
{
    private readonly IReadOnlyList<IComponentAttribute> _attributes;
    private readonly IReadOnlyDictionary<string, object?> _values;

    /// <summary>Creates an empty attribute collection.</summary>
    public ComponentAttributes()
        : this(Array.Empty<IComponentAttribute>())
    {
    }

    /// <summary>Creates an immutable snapshot of <paramref name="attributes"/>.</summary>
    /// <param name="attributes">The ordered attributes.</param>
    public ComponentAttributes(IEnumerable<IComponentAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        List<IComponentAttribute> snapshot = new();
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        foreach (IComponentAttribute attribute in attributes)
        {
            ArgumentNullException.ThrowIfNull(attribute);
            snapshot.Add(attribute);
            values[attribute.Name] = attribute.Value;
        }

        _attributes = snapshot.AsReadOnly();
        _values = values;
    }

    /// <inheritdoc/>
    public int Count => _attributes.Count;

    /// <inheritdoc/>
    public IComponentAttribute this[int index] => _attributes[index];

    /// <inheritdoc/>
    public bool TryGetValue(string name, out object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _values.TryGetValue(name, out value);
    }

    /// <inheritdoc/>
    public IEnumerator<IComponentAttribute> GetEnumerator() => _attributes.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

