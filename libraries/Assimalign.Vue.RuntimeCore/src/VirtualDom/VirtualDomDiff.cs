namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public static class VirtualDomDiff
{
    public static IReadOnlyList<VirtualDomPatch> Diff(VNode? current, VNode? next)
    {
        var patches = new List<VirtualDomPatch>();
        DiffNode(NodePath.Root, current, next, patches);
        return patches;
    }

    private static void DiffNode(NodePath path, VNode? current, VNode? next, IList<VirtualDomPatch> patches)
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
            case VText currentText when next is VText nextText:
                if (!string.Equals(currentText.Content, nextText.Content, StringComparison.Ordinal))
                {
                    patches.Add(new SetTextPatch(path, nextText.Content));
                }

                break;
            case VElement currentElement when next is VElement nextElement:
                DiffProperties(path, currentElement, nextElement, patches);
                DiffChildren(path, currentElement.Children, nextElement.Children, patches);
                break;
            case VFragment currentFragment when next is VFragment nextFragment:
                DiffChildren(path, currentFragment.Children, nextFragment.Children, patches);
                break;
        }
    }

    private static void DiffProperties(
        NodePath path,
        VElement current,
        VElement next,
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
        IReadOnlyList<VNode> currentChildren,
        IReadOnlyList<VNode> nextChildren,
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

    private static bool CanPatch(VNode current, VNode next)
    {
        if (!string.Equals(current.Key, next.Key, StringComparison.Ordinal))
        {
            return false;
        }

        return current switch
        {
            VElement currentElement when next is VElement nextElement =>
                string.Equals(currentElement.TagName, nextElement.TagName, StringComparison.Ordinal),
            VText when next is VText => true,
            VFragment when next is VFragment => true,
            _ => false
        };
    }
}
