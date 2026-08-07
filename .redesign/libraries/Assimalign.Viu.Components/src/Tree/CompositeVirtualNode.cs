using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a virtual node whose ordinary structure is an ordered child collection.
/// </summary>
public abstract class CompositeVirtualNode : VirtualNode
{
    private protected CompositeVirtualNode(
        VirtualNodeKind kind,
        IEnumerable<VirtualNode>? children,
        object? key,
        MountReference? mountReference,
        RenderPlan? renderPlan)
        : base(kind, key, mountReference, renderPlan)
    {
        Children = CollectionSnapshot.Copy(children);
    }

    /// <summary>Gets the immutable snapshot of child descriptions.</summary>
    public IReadOnlyList<VirtualNode> Children { get; }
}
