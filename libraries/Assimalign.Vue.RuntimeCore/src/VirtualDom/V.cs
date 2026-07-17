using System.Collections.ObjectModel;

namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public static class V
{
    public static VElement H(string tagName, params VNode[] children)
    {
        return new VElement(tagName, children: children);
    }

    public static VElement H(string tagName, IReadOnlyDictionary<string, object?> properties, params VNode[] children)
    {
        return new VElement(tagName, properties, children);
    }

    public static VElement H(
        string tagName,
        IReadOnlyDictionary<string, object?>? properties,
        IEnumerable<VNode> children,
        string? key = null)
    {
        return new VElement(tagName, properties, children, key);
    }

    public static VFragment Fragment(params VNode[] children)
    {
        return new VFragment(children);
    }

    public static VFragment Fragment(IEnumerable<VNode> children, string? key = null)
    {
        return new VFragment(children, key);
    }

    public static VText Text(string content, string? key = null)
    {
        return new VText(content, key);
    }

    public static IReadOnlyDictionary<string, object?> Props(params (string Name, object? Value)[] entries)
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

    public static VEventHandler On(Action callback)
    {
        return new VEventHandler(callback);
    }
}
