using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Supplies host-node operations used by Core's persistent renderer.
/// </summary>
/// <typeparam name="TNode">The opaque host node type.</typeparam>
public interface IVirtualNodeHost<TNode>
    where TNode : class
{
    /// <summary>Creates an element preserving its qualified name.</summary>
    TNode CreateElement(QualifiedName name);

    /// <summary>Creates a text node.</summary>
    TNode CreateText(string text);

    /// <summary>Creates a comment node.</summary>
    TNode CreateComment(string text);

    /// <summary>Creates a renderer-owned detached container for control-node algorithms.</summary>
    TNode CreateDetachedContainer();

    /// <summary>Resolves a teleport target identifier according to host policy.</summary>
    TNode ResolveTarget(string targetIdentifier);

    /// <summary>Inserts a child before an optional anchor.</summary>
    void Insert(TNode child, TNode parent, TNode? anchor);

    /// <summary>Removes a node and any host resources directly owned by it.</summary>
    void Remove(TNode node);

    /// <summary>Replaces the payload of a text node.</summary>
    void SetText(TNode node, string text);

    /// <summary>Applies, updates, or removes one explicitly classified element binding.</summary>
    void PatchBinding(TNode element, ElementBinding? previous, ElementBinding? current);

    /// <summary>Gets the current parent node, or null for a detached node.</summary>
    TNode? GetParent(TNode node);

    /// <summary>Gets the current following sibling, or null when none exists.</summary>
    TNode? GetNextSibling(TNode node);
}
