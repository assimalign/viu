using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Vue.RuntimeCore;

public static class VirtualNodeFactory
{
    public static VirtualElement Element(string tagName, params VirtualNode[] children)
    {
        return new VirtualElement(tagName, children: children);
    }

    public static VirtualElement Element(string tagName, IReadOnlyDictionary<string, object?> properties, params VirtualNode[] children)
    {
        return new VirtualElement(tagName, properties, children);
    }

    public static VirtualElement Element(
        string tagName,
        IReadOnlyDictionary<string, object?>? properties,
        IEnumerable<VirtualNode> children,
        string? key = null)
    {
        return new VirtualElement(tagName, properties, children, key);
    }

    public static VirtualFragment Fragment(params VirtualNode[] children)
    {
        return new VirtualFragment(children);
    }

    public static VirtualFragment Fragment(IEnumerable<VirtualNode> children, string? key = null)
    {
        return new VirtualFragment(children, key);
    }

    public static VirtualText Text(string content, string? key = null)
    {
        return new VirtualText(content, key);
    }

    public static IReadOnlyDictionary<string, object?> Properties(params (string Name, object? Value)[] entries)
    {
        var properties = new Dictionary<string, object?>(entries.Length, StringComparer.Ordinal);

        foreach (var (name, value) in entries)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Property names cannot be empty.", nameof(entries));
            }

            properties[name] = value;
        }

        return new ReadOnlyDictionary<string, object?>(properties);
    }

    public static VirtualEventHandler On(Action callback)
    {
        return new VirtualEventHandler(callback);
    }
}
