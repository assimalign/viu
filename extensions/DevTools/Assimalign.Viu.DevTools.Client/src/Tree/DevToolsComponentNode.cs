using System.Collections.Generic;

namespace Assimalign.Viu.DevTools.Client;

/// <summary>A client-owned component identity updated in place from protocol data. Single-threaded. Specified by <c>[DVT-14]</c>.</summary>
public sealed class DevToolsComponentNode
{
    internal DevToolsComponentNode(int identifier, string name, string? key, int? parentIdentifier)
    {
        Identifier = identifier;
        Name = name;
        Key = key;
        ParentIdentifier = parentIdentifier;
    }

    internal List<DevToolsComponentNode> ChildNodes { get; } = [];
    /// <summary>Gets the connection-local component identity.</summary>
    public int Identifier { get; }
    /// <summary>Gets the latest display name.</summary>
    public string Name { get; internal set; }
    /// <summary>Gets the reconciliation key's display value.</summary>
    public string? Key { get; internal set; }
    /// <summary>Gets the parent identity, or null for a root.</summary>
    public int? ParentIdentifier { get; internal set; }
    /// <summary>Gets ordered children; keyed moves preserve these node objects.</summary>
    public IReadOnlyList<DevToolsComponentNode> Children => ChildNodes;
}
