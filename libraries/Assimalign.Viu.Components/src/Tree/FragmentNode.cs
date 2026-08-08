using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a transparent ordered child collection in the closed algebra specified by
/// <c>[CMP-3]</c>.
/// </summary>
public sealed class FragmentNode : CompositeVirtualNode
{
    /// <summary>Initializes an immutable fragment.</summary>
    /// <param name="children">The ordered children.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="renderPlan">Optional compiler patch information.</param>
    public FragmentNode(
        IEnumerable<VirtualNode>? children = null,
        object? key = null,
        RenderPlan? renderPlan = null)
        : base(VirtualNodeKind.Fragment, children, key, null, renderPlan)
    {
    }
}
