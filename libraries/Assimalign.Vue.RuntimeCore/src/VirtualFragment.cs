using System;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Vue.RuntimeCore;

public sealed class VirtualFragment : VirtualNode
{
    private static readonly IReadOnlyList<VirtualNode> EmptyChildren = Array.Empty<VirtualNode>();

    public VirtualFragment(IEnumerable<VirtualNode>? children = null, string? key = null)
        : base(key)
    {
        Children = children is null ? EmptyChildren : children.ToArray();
    }

    public override VirtualNodeKind Kind => VirtualNodeKind.Fragment;

    public IReadOnlyList<VirtualNode> Children { get; }
}
