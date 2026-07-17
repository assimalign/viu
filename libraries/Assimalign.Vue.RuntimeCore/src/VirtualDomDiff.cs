using System;
using System.Collections.Generic;

namespace Assimalign.Vue.RuntimeCore;

public static class VirtualDomDiff
{
    public static IReadOnlyList<VirtualDomPatch> Diff(VirtualNode? current, VirtualNode? next)
    {
        var patches = new List<VirtualDomPatch>();
        DiffNode(NodePath.Root, current, next, patches);
        return patches;
    }

    private static void DiffNode(NodePath path, VirtualNode? current, VirtualNode? next, IList<VirtualDomPatch> patches)
    {
        if (current is null && next is null)
        {
            return;
        }

        if (current is null && next is not null)
        {
            patches.Add(new ReplaceNodePatch(path, next));
            return;
        }

        if (current is not null && next is null)
        {
            patches.Add(new RemoveNodePatch(path));
            return;
        }

        if (current is null || next is null)
        {
            return;
        }

        if (!CanPatch(current, next))
        {
            patches.Add(new ReplaceNodePatch(path, next));
            return;
        }

        switch (current)
        {
            case VirtualText currentText when next is VirtualText nextText:
                if (!string.Equals(currentText.Content, nextText.Content, StringComparison.Ordinal))
                {
                    patches.Add(new SetTextPatch(path, nextText.Content));
                }

                break;
            case VirtualElement currentElement when next is VirtualElement nextElement:
                DiffProperties(path, currentElement, nextElement, patches);
                DiffChildren(path, currentElement.Children, nextElement.Children, patches);
                break;
            case VirtualFragment currentFragment when next is VirtualFragment nextFragment:
                DiffChildren(path, currentFragment.Children, nextFragment.Children, patches);
                break;
        }
    }

    private static void DiffProperties(
        NodePath path,
        VirtualElement current,
        VirtualElement next,
        IList<VirtualDomPatch> patches)
    {
        foreach (var property in current.Properties)
        {
            if (!next.Properties.ContainsKey(property.Key))
            {
                patches.Add(new RemovePropertyPatch(path, property.Key));
            }
        }

        foreach (var property in next.Properties)
        {
            if (!current.Properties.TryGetValue(property.Key, out var currentValue) ||
                !Equals(currentValue, property.Value))
            {
                patches.Add(new SetPropertyPatch(path, property.Key, property.Value));
            }
        }
    }

    private static void DiffChildren(
        NodePath path,
        IReadOnlyList<VirtualNode> currentChildren,
        IReadOnlyList<VirtualNode> nextChildren,
        IList<VirtualDomPatch> patches)
    {
        var sharedCount = Math.Min(currentChildren.Count, nextChildren.Count);

        for (var index = 0; index < sharedCount; index++)
        {
            DiffNode(path.Append(index), currentChildren[index], nextChildren[index], patches);
        }

        for (var index = sharedCount; index < nextChildren.Count; index++)
        {
            patches.Add(new InsertChildPatch(path, index, nextChildren[index]));
        }

        for (var index = currentChildren.Count - 1; index >= nextChildren.Count; index--)
        {
            patches.Add(new RemoveChildPatch(path, index));
        }
    }

    private static bool CanPatch(VirtualNode current, VirtualNode next)
    {
        if (!string.Equals(current.Key, next.Key, StringComparison.Ordinal))
        {
            return false;
        }

        return current switch
        {
            VirtualElement currentElement when next is VirtualElement nextElement =>
                string.Equals(currentElement.TagName, nextElement.TagName, StringComparison.Ordinal),
            VirtualText when next is VirtualText => true,
            VirtualFragment when next is VirtualFragment => true,
            _ => false
        };
    }
}
