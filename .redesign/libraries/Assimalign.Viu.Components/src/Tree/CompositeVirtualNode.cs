using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a virtual node whose ordinary structure is an ordered child collection.
/// </summary>
/// <remarks>Children are copied into an immutable snapshot as required by <c>[CMP-2]</c>.</remarks>
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
        Children = CollectionSnapshot.CopyNonNull(children, nameof(children));
    }

    /// <summary>Gets the immutable snapshot of child descriptions.</summary>
    public IReadOnlyList<VirtualNode> Children { get; }
}
