using System;
using System.Collections.Generic;

namespace Assimalign.Viu.DevTools;

/// <summary>Describes one immutable node returned by a custom inspector tree provider.</summary>
public sealed class DevToolsInspectorNode
{
    /// <summary>Initializes a custom inspector node.</summary>
    /// <param name="identifier">The stable identifier within the inspector.</param>
    /// <param name="label">The human-readable node label.</param>
    /// <param name="children">Optional child nodes in display order.</param>
    public DevToolsInspectorNode(
        string identifier,
        string label,
        IReadOnlyList<DevToolsInspectorNode>? children = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        ArgumentException.ThrowIfNullOrEmpty(label);
        Identifier = identifier;
        Label = label;
        Children = children is null
            ? Array.Empty<DevToolsInspectorNode>()
            : new List<DevToolsInspectorNode>(children).AsReadOnly();
    }

    /// <summary>Gets the stable identifier within its inspector.</summary>
    public string Identifier { get; }

    /// <summary>Gets the human-readable node label.</summary>
    public string Label { get; }

    /// <summary>Gets child nodes in display order.</summary>
    public IReadOnlyList<DevToolsInspectorNode> Children { get; }
}
