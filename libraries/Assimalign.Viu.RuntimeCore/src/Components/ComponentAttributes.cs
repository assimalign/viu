using System;
using System.Collections.Generic;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The instance's fallthrough attributes — undeclared vnode props minus declared emits'
/// handler props (upstream: <c>instance.attrs</c>, https://vuejs.org/guide/components/attrs.html).
/// A live view: the owner replaces the content on every parent patch, so reads always see the
/// latest values. Consumed by <see cref="ComponentSetupContext.Attributes"/> and by the
/// renderer's single-root fallthrough merge.
/// </summary>
public sealed class ComponentAttributes
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    internal ComponentAttributes()
    {
    }

    /// <summary>The number of fallthrough attributes.</summary>
    public int Count => _values.Count;

    /// <summary>Reads an attribute, or null when absent.</summary>
    /// <param name="name">The attribute name.</param>
    public object? this[string name]
    {
        get
        {
            _values.TryGetValue(name, out var value);
            return value;
        }
    }

    /// <summary>Whether the attribute is present.</summary>
    /// <param name="name">The attribute name.</param>
    public bool Contains(string name) => _values.ContainsKey(name);

    /// <summary>Enumerates the current name/value pairs.</summary>
    public Dictionary<string, object?>.Enumerator GetEnumerator() => _values.GetEnumerator();

    internal void ReplaceFrom(List<KeyValuePair<string, object?>>? entries)
    {
        _values.Clear();
        if (entries is null)
        {
            return;
        }
        foreach (var (name, value) in entries)
        {
            _values[name] = value;
        }
    }

    internal VirtualNodeProperties? ToProperties()
    {
        if (_values.Count == 0)
        {
            return null;
        }
        var properties = new VirtualNodeProperties(_values.Count);
        foreach (var (name, value) in _values)
        {
            properties.Set(name, value);
        }
        return properties;
    }
}
