using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Testing;

/// <summary>
/// An in-memory element node: tag, properties, children, and the event listeners registered
/// through <c>patchProp</c> (mirrors <c>TestElement</c> in <c>@vue/runtime-test</c>).
/// </summary>
public sealed class TestElement : TestNode
{
    internal TestElement(string tag, string? elementNamespace)
    {
        Tag = tag;
        Namespace = elementNamespace;
    }

    /// <summary>The element tag.</summary>
    public string Tag { get; }

    /// <summary>The namespace the element was created in (<c>"svg"</c>, <c>"mathml"</c>, or null for HTML).</summary>
    public string? Namespace { get; }

    /// <summary>The element's properties as last patched.</summary>
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);

    /// <summary>The child nodes, in document order.</summary>
    public List<TestNode> Children { get; } = [];

    /// <summary>
    /// The event listeners registered through <c>patchProp</c>, keyed by lower-case event name
    /// (an <c>onClick</c> prop registers under <c>"click"</c>).
    /// </summary>
    public Dictionary<string, Delegate> EventListeners { get; } = new(StringComparer.Ordinal);
}
