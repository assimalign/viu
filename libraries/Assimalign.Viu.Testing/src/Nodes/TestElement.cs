using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>Represents an in-memory element and its host bindings, listeners, and children.</summary>
/// <remarks>Specified by <c>[RND-HOST-1]</c>, <c>[RND-HOST-3]</c>, and <c>[CONF-3]</c>.</remarks>
public sealed class TestElement : TestNode
{
    internal TestElement(QualifiedName name)
    {
        Name = name;
    }

    /// <summary>Gets the complete qualified element name supplied by the renderer.</summary>
    public QualifiedName Name { get; }

    /// <summary>Gets the local element name.</summary>
    public string Tag => Name.LocalName;

    /// <summary>Gets the optional namespace name.</summary>
    public string? Namespace => Name.NamespaceName;

    /// <summary>Gets the host attributes and properties as last patched.</summary>
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets child nodes in host order.</summary>
    public List<TestNode> Children { get; } = [];

    /// <summary>Gets event listeners keyed by the event binding's local name.</summary>
    public Dictionary<string, Delegate> EventListeners { get; } = new(StringComparer.Ordinal);
}
