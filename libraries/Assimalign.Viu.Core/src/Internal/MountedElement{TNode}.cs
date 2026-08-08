using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedElement<TNode> : MountedNode<TNode>
    where TNode : notnull
{
    internal MountedElement(
        ElementNode value,
        TNode hostNode,
        List<MountedNode<TNode>> children,
        List<DirectiveBinding> directiveBindings,
        RuntimeComponentContext? owner)
        : base(value, owner)
    {
        HostNode = hostNode;
        Children = children;
        DirectiveBindings = directiveBindings;
    }

    internal TNode HostNode;

    internal List<MountedNode<TNode>> Children;

    internal List<DirectiveBinding> DirectiveBindings;

    internal override TNode FirstHostNode => HostNode;

    internal override TNode LastHostNode => HostNode;
}
