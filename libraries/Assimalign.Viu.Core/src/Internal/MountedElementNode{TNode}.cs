using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class MountedElementNode<TNode> : MountedRenderNode<TNode>
    where TNode : notnull
{
    internal MountedElementNode(
        IElementComponent component,
        TNode hostNode,
        List<MountedRenderNode<TNode>> children,
        List<DirectiveBinding> directiveBindings,
        ComponentContext? owner)
        : base(component, owner)
    {
        HostNode = hostNode;
        Children = children;
        DirectiveBindings = directiveBindings;
    }

    internal TNode HostNode;

    internal List<MountedRenderNode<TNode>> Children;

    internal List<DirectiveBinding> DirectiveBindings;

    internal TransitionHooks? Transition;

    internal override TNode FirstHostNode => HostNode;

    internal override TNode LastHostNode => HostNode;
}
