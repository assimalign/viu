using System.Collections.Generic;

namespace Assimalign.Viu;

public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private static IReadOnlyList<KeyedComponentHostElementSnapshot>
        GetKeyedChildElementSnapshots(
            MountedRenderNode<TNode> root)
    {
        IReadOnlyList<MountedRenderNode<TNode>> children =
            root switch
            {
                MountedElementNode<TNode> element => element.Children,
                MountedFragmentNode<TNode> fragment => fragment.Children,
                _ => [],
            };
        if (children.Count == 0)
        {
            return [];
        }

        List<KeyedComponentHostElementSnapshot> snapshots = [];
        for (int index = 0; index < children.Count; index++)
        {
            MountedRenderNode<TNode> child = children[index];
            object? key = child.Component.Key;
            if (key is null
                || !TryGetFirstHostElement(
                    child,
                    out TNode element))
            {
                continue;
            }

            snapshots.Add(
                new KeyedComponentHostElementSnapshot(
                    child.Component,
                    key,
                    element));
        }

        return snapshots.AsReadOnly();
    }

    private static bool TryGetFirstHostElement(
        MountedRenderNode<TNode> mounted,
        out TNode element)
    {
        switch (mounted)
        {
            case MountedElementNode<TNode> mountedElement:
                element = mountedElement.HostNode;
                return true;
            case MountedTemplateNode<TNode> template:
                return TryGetFirstHostElement(
                    template.Subtree,
                    out element);
            case MountedFragmentNode<TNode> fragment:
                for (int index = 0;
                    index < fragment.Children.Count;
                    index++)
                {
                    if (TryGetFirstHostElement(
                        fragment.Children[index],
                        out element))
                    {
                        return true;
                    }
                }

                break;
        }

        element = default!;
        return false;
    }
}
