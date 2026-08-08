using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Mounts, patches, moves, and unmounts immutable virtual trees through host-supplied operations.
/// </summary>
/// <typeparam name="TNode">The opaque host-node type.</typeparam>
/// <remarks>
/// The renderer owns a parallel sealed mounted hierarchy and never writes mounted state onto a
/// <see cref="VirtualNode"/>. It is intentionally single-threaded. Specified by <c>[RND-1]</c>
/// through <c>[RND-6]</c>.
/// </remarks>
public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private static readonly EqualityComparer<TNode> NodeComparer =
        EqualityComparer<TNode>.Default;

    private readonly Dictionary<TNode, MountedTree<TNode>> _containerTrees =
        new(NodeComparer);
    private readonly RendererOptions<TNode> _options;
    private TransitionMountContext<TNode>? _activeTransitionMount;
    private int _nextComponentIdentifier;

    internal Renderer(RendererOptions<TNode> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Insert);
        ArgumentNullException.ThrowIfNull(options.Remove);
        ArgumentNullException.ThrowIfNull(options.CreateElement);
        ArgumentNullException.ThrowIfNull(options.CreateText);
        ArgumentNullException.ThrowIfNull(options.CreateComment);
        ArgumentNullException.ThrowIfNull(options.SetText);
        ArgumentNullException.ThrowIfNull(options.ParentNode);
        ArgumentNullException.ThrowIfNull(options.NextSibling);
        ArgumentNullException.ThrowIfNull(options.PatchAttribute);
        _options = options;
    }

    /// <summary>
    /// Gets or sets the number of patch dispatches observed by the test-host characterization
    /// seam. Production code does not use this counter. Specified by <c>[RND-BLOCK-4]</c>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int PatchVisitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of unmount dispatches observed by the test-host characterization
    /// seam. Production code does not use this counter. Specified by <c>[RND-BLOCK-7]</c>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int UnmountVisitCount { get; set; }

    /// <summary>
    /// Reconciles a fresh immutable tree into a host container. Passing null unmounts and forgets
    /// the current root. The first application context supplied to a mounted container is sticky.
    /// </summary>
    /// <param name="value">The next immutable root, or null to unmount.</param>
    /// <param name="container">The host container.</param>
    /// <param name="application">The optional application composition used for component nodes.</param>
    /// <returns>The root component context, or null when the root is not an authored component.</returns>
    /// <remarks>Specified by <c>[RND-5]</c>.</remarks>
    public ComponentContext? Render(
        VirtualNode? value,
        TNode container,
        IApplicationContext? application = null)
    {
        RequireHostNode(container, nameof(container));
        _containerTrees.TryGetValue(container, out MountedTree<TNode>? tree);

        if (value is null)
        {
            if (tree?.Root is not null)
            {
                List<MountedTransition<TNode>> transitions =
                    new(tree.PendingTransitionRemovals);
                for (int index = 0; index < transitions.Count; index++)
                {
                    MountedTransition<TNode> transition = transitions[index];
                    if (!transition.IsUnmounted)
                    {
                        Unmount(tree, transition, removeHostNodes: true);
                    }
                }

                Unmount(tree, tree.Root, removeHostNodes: true);
                tree.Nodes.Clear();
                tree.Root = null;
                _containerTrees.Remove(container);
            }

            QueueHostCommit();
            Scheduler.FlushAfterSynchronousRender();
            return null;
        }

        Scheduler.FlushPreFlushCallbacks();
        if (tree is null)
        {
            tree = new MountedTree<TNode>
            {
                Application = application,
            };
            tree.Root = Mount(tree, value, container, default, owner: null);
            _containerTrees.Add(container, tree);
        }
        else
        {
            if (application is not null
                && tree.Application is not null
                && !ReferenceEquals(application, tree.Application))
            {
                throw new InvalidOperationException(
                    "A mounted container cannot change its application context.");
            }

            tree.Application ??= application;
            tree.Root = Patch(
                tree,
                tree.Root,
                value,
                container,
                default,
                owner: null);
        }

        NormalizeTeleportTargetOrder(tree);
        QueueHostCommit();
        Scheduler.FlushAfterSynchronousRender();
        return tree.Root is MountedComponent<TNode> component
            ? component.Context
            : null;
    }

    /// <summary>
    /// Returns the engine-cached cold-path views of all currently mounted authored components.
    /// Repeated queries return the same view instance for the life of each mount.
    /// </summary>
    /// <param name="container">The rendered host container.</param>
    /// <returns>The current component views in structural order.</returns>
    /// <remarks>Specified by <c>[RND-6]</c>.</remarks>
    public IReadOnlyList<MountedComponentView<TNode>> GetMountedComponentViews(TNode container)
    {
        RequireHostNode(container, nameof(container));
        if (!_containerTrees.TryGetValue(container, out MountedTree<TNode>? tree)
            || tree.Root is null)
        {
            return Array.Empty<MountedComponentView<TNode>>();
        }

        List<MountedComponentView<TNode>> views = [];
        CollectMountedComponentViews(tree.Root, views);
        return views.AsReadOnly();
    }

    private static void CollectMountedComponentViews(
        MountedNode<TNode> mounted,
        List<MountedComponentView<TNode>> views)
    {
        switch (mounted)
        {
            case MountedComponent<TNode> component:
                views.Add(component.View);
                CollectMountedComponentViews(component.Subtree, views);
                break;
            case MountedElement<TNode> element:
                CollectMountedComponentViews(element.Children, views);
                break;
            case MountedRange<TNode> range:
                CollectMountedComponentViews(range.Children, views);
                break;
            case MountedTeleport<TNode> teleport:
                CollectMountedComponentViews(teleport.Children, views);
                break;
            case MountedKeepAlive<TNode> keepAlive:
                keepAlive.CollectViews(views);
                break;
            case MountedSuspense<TNode> suspense:
                CollectMountedComponentViews(suspense.ActiveBranch, views);
                break;
            case MountedTransition<TNode> transition:
                CollectMountedComponentViews(transition.Child, views);
                break;
        }
    }

    private static void CollectMountedComponentViews(
        IReadOnlyList<MountedNode<TNode>> children,
        List<MountedComponentView<TNode>> views)
    {
        for (int index = 0; index < children.Count; index++)
        {
            CollectMountedComponentViews(children[index], views);
        }
    }

    private MountedNode<TNode> Patch(
        MountedTree<TNode> tree,
        MountedNode<TNode>? current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner,
        bool allowTransitionLeave = true)
    {
        PatchVisitCount++;
        if (current is null)
        {
            return Mount(tree, next, container, anchor, owner);
        }

        if (ReferenceEquals(current.Value, next))
        {
            return current;
        }

        if (!IsSameNodeType(current.Value, next))
        {
            TNode? replacementAnchor = GetNextHostNode(current);
            RuntimeComponentContext? replacementOwner = current.Owner;
            if (allowTransitionLeave)
            {
                Remove(tree, current);
            }
            else
            {
                Unmount(tree, current, removeHostNodes: true);
            }

            return Mount(tree, next, container, replacementAnchor, replacementOwner);
        }

        switch (current, next)
        {
            case (MountedElement<TNode> element, ElementNode elementValue):
                PatchElement(tree, element, elementValue);
                break;
            case (MountedLeaf<TNode> text, TextNode textValue):
                PatchText(tree, text, textValue);
                break;
            case (MountedLeaf<TNode> comment, CommentNode commentValue):
                PatchComment(tree, comment, commentValue);
                break;
            case (MountedStatic<TNode> staticContent, StaticNode staticValue):
                ReplaceValue(tree, staticContent, staticValue);
                break;
            case (MountedRange<TNode> fragment, FragmentNode fragmentValue):
                PatchFragment(tree, fragment, fragmentValue, container);
                break;
            case (MountedComponent<TNode> component, ComponentNode componentValue):
                PatchComponent(tree, component, componentValue, container);
                break;
            case (MountedTeleport<TNode> teleport, TeleportNode teleportValue):
                PatchTeleport(tree, teleport, teleportValue, container);
                break;
            case (MountedKeepAlive<TNode> keepAlive, KeepAliveNode keepAliveValue):
                PatchKeepAlive(tree, keepAlive, keepAliveValue, container);
                break;
            case (MountedSuspense<TNode> suspense, SuspenseNode suspenseValue):
                PatchSuspense(tree, suspense, suspenseValue, container);
                break;
            case (MountedTransition<TNode> transition, TransitionNode transitionValue):
                PatchTransition(tree, transition, transitionValue, container);
                break;
            default:
                throw new InvalidOperationException(
                    "The mounted hierarchy no longer matches the closed virtual-node algebra.");
        }

        return current;
    }

    private MountedNode<TNode> Mount(
        MountedTree<TNode> tree,
        VirtualNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        return value switch
        {
            ElementNode element => MountElement(tree, element, container, anchor, owner),
            TextNode text => MountText(tree, text, container, anchor, owner),
            CommentNode comment => MountComment(tree, comment, container, anchor, owner),
            StaticNode staticContent => MountStatic(tree, staticContent, container, anchor, owner),
            FragmentNode fragment => MountFragment(tree, fragment, container, anchor, owner),
            ComponentNode component => MountComponent(tree, component, container, anchor, owner),
            TeleportNode teleport => MountTeleport(tree, teleport, container, anchor, owner),
            KeepAliveNode keepAlive => MountKeepAlive(tree, keepAlive, container, anchor, owner),
            SuspenseNode suspense => MountSuspense(tree, suspense, container, anchor, owner),
            TransitionNode transition => MountTransition(tree, transition, container, anchor, owner),
            _ => throw new InvalidOperationException("Unknown virtual-node variant."),
        };
    }

    private MountedElement<TNode> MountElement(
        MountedTree<TNode> tree,
        ElementNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        TNode element = _options.CreateElement(value.Name);
        List<DirectiveBinding> directiveBindings = ResolveDirectiveBindings(
            tree,
            value.Directives,
            owner);
        BindActiveTransition(tree, element, directiveBindings);
        InvokeDirectiveHooks(
            tree,
            element,
            directiveBindings,
            value,
            previousValue: null,
            DirectiveHookKind.Created);
        MountAttributes(element, value.Bindings);
        List<MountedNode<TNode>> children = MountChildren(
            tree,
            value.Children,
            element,
            default,
            owner);
        InvokeVirtualNodeLifecycleHook(
            tree,
            owner,
            value,
            previousValue: null,
            "onVnodeBeforeMount");
        InvokeDirectiveHooks(
            tree,
            element,
            directiveBindings,
            value,
            previousValue: null,
            DirectiveHookKind.BeforeMount);
        _options.Insert(element, container, anchor);
        MountedElement<TNode> mounted = new(
            value,
            element,
            children,
            directiveBindings,
            owner);
        BindDirectiveHostElements(mounted, directiveBindings);
        Register(tree, value, mounted);
        UpdateReference(tree, mounted, null, value.MountReference);
        QueueVirtualNodeLifecycleHook(
            tree,
            owner,
            mounted,
            value,
            previousValue: null,
            "onVnodeMounted");
        QueueDirectiveHooks(
            tree,
            mounted,
            value,
            previousValue: null,
            DirectiveHookKind.Mounted);
        return mounted;
    }

    private MountedLeaf<TNode> MountText(
        MountedTree<TNode> tree,
        TextNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        TNode hostNode = _options.CreateText(value.Text);
        _options.Insert(hostNode, container, anchor);
        MountedLeaf<TNode> mounted = new(value, hostNode, owner);
        Register(tree, value, mounted);
        UpdateReference(tree, mounted, null, value.MountReference);
        return mounted;
    }

    private MountedLeaf<TNode> MountComment(
        MountedTree<TNode> tree,
        CommentNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        TNode hostNode = _options.CreateComment(value.Text);
        _options.Insert(hostNode, container, anchor);
        MountedLeaf<TNode> mounted = new(value, hostNode, owner);
        Register(tree, value, mounted);
        return mounted;
    }

    private MountedStatic<TNode> MountStatic(
        MountedTree<TNode> tree,
        StaticNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        InsertStaticContentDelegate<TNode> insert = _options.InsertStaticContent
            ?? throw new NotSupportedException(
                "The active host does not support static-content insertion.");
        (TNode first, TNode last) = insert(value.Format, value.Content, container, anchor);
        RequireHostNode(first, "first static host node");
        RequireHostNode(last, "last static host node");
        MountedStatic<TNode> mounted = new(value, first, last, owner);
        Register(tree, value, mounted);
        return mounted;
    }

    private MountedRange<TNode> MountFragment(
        MountedTree<TNode> tree,
        FragmentNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        TNode startAnchor = _options.CreateComment(HydrationMarkers.FragmentStartData);
        TNode endAnchor = _options.CreateComment(HydrationMarkers.FragmentEndData);
        _options.Insert(startAnchor, container, anchor);
        _options.Insert(endAnchor, container, anchor);
        List<MountedNode<TNode>> children = MountChildren(
            tree,
            value.Children,
            container,
            endAnchor,
            owner);
        MountedRange<TNode> mounted = new(
            value,
            startAnchor,
            endAnchor,
            children,
            owner);
        Register(tree, value, mounted);
        return mounted;
    }

    private void PatchElement(
        MountedTree<TNode> tree,
        MountedElement<TNode> mounted,
        ElementNode next)
    {
        ElementNode previous = (ElementNode)mounted.Value;
        PatchFlags flags = next.RenderPlan.PatchFlags;
        if (flags == PatchFlags.Cached)
        {
            UpdateReference(tree, mounted, previous.MountReference, next.MountReference);
            ReplaceValue(tree, mounted, next);
            return;
        }

        List<DirectiveBinding> nextDirectiveBindings = ResolveDirectiveBindings(
            tree,
            next.Directives,
            mounted.Owner,
            mounted.DirectiveBindings);
        BindActiveTransition(tree, mounted.HostNode, nextDirectiveBindings);
        InvokeVirtualNodeLifecycleHook(
            tree,
            mounted.Owner,
            next,
            previous,
            "onVnodeBeforeUpdate");
        InvokeDirectiveHooks(
            tree,
            mounted.HostNode,
            nextDirectiveBindings,
            next,
            previous,
            DirectiveHookKind.BeforeUpdate);

        bool blockPatched = flags != PatchFlags.Bail
            && TryPatchBlockChildren(tree, previous, next, mounted.HostNode);
        bool incompatibleBlock = !blockPatched
            && (previous.RenderPlan.IsBlock || next.RenderPlan.IsBlock);
        if (incompatibleBlock)
        {
            PatchAttributes(mounted.HostNode, previous.Bindings, next.Bindings);
            mounted.Children = PatchChildren(
                tree,
                mounted.Children,
                previous.Children,
                next.Children,
                mounted.HostNode,
                default,
                mounted.Owner,
                PatchFlags.Bail);
        }
        else if (blockPatched)
        {
            PatchElementAttributes(mounted.HostNode, previous, next);
            PatchElementText(tree, mounted, previous, next);
            CarryForwardStaticChildren(
                tree,
                previous.Children,
                next.Children,
                mounted.Children);
        }
        else if ((int)flags > 0)
        {
            PatchElementAttributes(mounted.HostNode, previous, next);
            PatchElementText(tree, mounted, previous, next);
        }
        else
        {
            PatchAttributes(mounted.HostNode, previous.Bindings, next.Bindings);
            mounted.Children = PatchChildren(
                tree,
                mounted.Children,
                previous.Children,
                next.Children,
                mounted.HostNode,
                default,
                mounted.Owner,
                flags);
        }

        UpdateReference(tree, mounted, previous.MountReference, next.MountReference);
        ReplaceValue(tree, mounted, next);
        mounted.DirectiveBindings = nextDirectiveBindings;
        BindDirectiveHostElements(mounted, nextDirectiveBindings);
        QueueVirtualNodeLifecycleHook(
            tree,
            mounted.Owner,
            mounted,
            next,
            previous,
            "onVnodeUpdated");
        QueueDirectiveHooks(
            tree,
            mounted,
            next,
            previous,
            DirectiveHookKind.Updated);
    }

    private void PatchElementText(
        MountedTree<TNode> tree,
        MountedElement<TNode> mounted,
        ElementNode previous,
        ElementNode next)
    {
        if ((next.RenderPlan.PatchFlags & PatchFlags.Text) == 0)
        {
            return;
        }

        if (previous.Children.Count == 1
            && next.Children.Count == 1
            && mounted.Children.Count == 1
            && next.Children[0] is TextNode)
        {
            if (!tree.Nodes.ContainsKey(next.Children[0]))
            {
                mounted.Children[0] = Patch(
                    tree,
                    mounted.Children[0],
                    next.Children[0],
                    mounted.HostNode,
                    default,
                    mounted.Owner);
            }

            return;
        }

        mounted.Children = PatchChildren(
            tree,
            mounted.Children,
            previous.Children,
            next.Children,
            mounted.HostNode,
            default,
            mounted.Owner,
            PatchFlags.Bail);
    }

    private void PatchText(
        MountedTree<TNode> tree,
        MountedLeaf<TNode> mounted,
        TextNode next)
    {
        TextNode previous = (TextNode)mounted.Value;
        if (!string.Equals(previous.Text, next.Text, StringComparison.Ordinal))
        {
            _options.SetText(mounted.HostNode, next.Text);
        }

        UpdateReference(tree, mounted, previous.MountReference, next.MountReference);
        ReplaceValue(tree, mounted, next);
    }

    private static void PatchComment(
        MountedTree<TNode> tree,
        MountedLeaf<TNode> mounted,
        CommentNode next)
    {
        ReplaceValue(tree, mounted, next);
    }

    private void PatchFragment(
        MountedTree<TNode> tree,
        MountedRange<TNode> mounted,
        FragmentNode next,
        TNode container)
    {
        FragmentNode previous = (FragmentNode)mounted.Value;
        PatchFlags flags = next.RenderPlan.PatchFlags;
        bool blockPatched = flags != PatchFlags.Bail
            && flags != PatchFlags.Cached
            && TryPatchBlockChildren(tree, previous, next, container);
        if (flags != PatchFlags.Cached && !blockPatched)
        {
            mounted.Children = PatchChildren(
                tree,
                mounted.Children,
                previous.Children,
                next.Children,
                container,
                mounted.EndAnchor,
                mounted.Owner,
                flags);
        }
        else if (blockPatched)
        {
            CarryForwardStaticChildren(
                tree,
                previous.Children,
                next.Children,
                mounted.Children);
        }

        ReplaceValue(tree, mounted, next);
    }

    private List<MountedNode<TNode>> MountChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<VirtualNode> values,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        List<MountedNode<TNode>> children = new(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            children.Add(Mount(tree, values[index], container, anchor, owner));
        }

        return children;
    }

    private List<MountedNode<TNode>> PatchChildren(
        MountedTree<TNode> tree,
        List<MountedNode<TNode>> current,
        IReadOnlyList<VirtualNode> previousValues,
        IReadOnlyList<VirtualNode> nextValues,
        TNode container,
        TNode? endAnchor,
        RuntimeComponentContext? owner,
        PatchFlags flags)
    {
        return (int)flags > 0 && (flags & PatchFlags.UnkeyedFragment) != 0
            ? PatchUnkeyedChildren(
                tree,
                current,
                nextValues,
                container,
                endAnchor,
                owner)
            : PatchKeyedChildren(
                tree,
                current,
                previousValues,
                nextValues,
                container,
                endAnchor,
                owner);
    }

    private List<MountedNode<TNode>> PatchUnkeyedChildren(
        MountedTree<TNode> tree,
        List<MountedNode<TNode>> current,
        IReadOnlyList<VirtualNode> nextValues,
        TNode container,
        TNode? endAnchor,
        RuntimeComponentContext? owner)
    {
        int commonCount = Math.Min(current.Count, nextValues.Count);
        List<MountedNode<TNode>> nextMounted = new(nextValues.Count);
        for (int index = 0; index < commonCount; index++)
        {
            TNode? anchor = index + 1 < current.Count
                ? current[index + 1].FirstHostNode
                : endAnchor;
            nextMounted.Add(Patch(
                tree,
                current[index],
                nextValues[index],
                container,
                anchor,
                owner));
        }

        for (int index = commonCount; index < nextValues.Count; index++)
        {
            nextMounted.Add(Mount(tree, nextValues[index], container, endAnchor, owner));
        }

        for (int index = commonCount; index < current.Count; index++)
        {
            Remove(tree, current[index]);
        }

        return nextMounted;
    }

    private List<MountedNode<TNode>> PatchKeyedChildren(
        MountedTree<TNode> tree,
        List<MountedNode<TNode>> current,
        IReadOnlyList<VirtualNode> previousValues,
        IReadOnlyList<VirtualNode> nextValues,
        TNode container,
        TNode? endAnchor,
        RuntimeComponentContext? owner)
    {
        Dictionary<object, int> keyToNextIndex = [];
        bool hasKeyed = false;
        bool hasKeyless = false;
        for (int index = 0; index < nextValues.Count; index++)
        {
            object? key = nextValues[index].Key;
            if (key is null)
            {
                if (nextValues[index] is not CommentNode)
                {
                    hasKeyless = true;
                }

                continue;
            }

            hasKeyed = true;
            if (!keyToNextIndex.TryAdd(key, index))
            {
                tree.Application?.WarnHandler?.Invoke(
                    $"Duplicate sibling key '{key}' was encountered; the first position wins.");
            }
        }

        if (hasKeyed && hasKeyless)
        {
            tree.Application?.WarnHandler?.Invoke(
                "A sibling collection mixes keyed and keyless non-comment nodes.");
        }

        MountedNode<TNode>?[] nextMounted = new MountedNode<TNode>?[nextValues.Count];
        int[] previousIndexByNext = new int[nextValues.Count];
        bool[] claimed = new bool[nextValues.Count];
        bool moved = false;
        int greatestNextIndex = 0;

        for (int previousIndex = 0; previousIndex < current.Count; previousIndex++)
        {
            MountedNode<TNode> previousMounted = current[previousIndex];
            VirtualNode previousValue = previousIndex < previousValues.Count
                ? previousValues[previousIndex]
                : previousMounted.Value;
            int nextIndex = -1;
            if (previousValue.Key is { } key
                && keyToNextIndex.TryGetValue(key, out int keyedIndex)
                && !claimed[keyedIndex]
                && IsSameNodeType(previousValue, nextValues[keyedIndex]))
            {
                nextIndex = keyedIndex;
            }
            else if (previousValue.Key is null)
            {
                nextIndex = FindNextKeylessIndex(
                    previousValue,
                    nextValues,
                    claimed);
            }

            if (nextIndex < 0)
            {
                Remove(tree, previousMounted);
                continue;
            }

            claimed[nextIndex] = true;
            previousIndexByNext[nextIndex] = previousIndex + 1;
            if (nextIndex < greatestNextIndex)
            {
                moved = true;
            }
            else
            {
                greatestNextIndex = nextIndex;
            }

            nextMounted[nextIndex] = Patch(
                tree,
                previousMounted,
                nextValues[nextIndex],
                container,
                endAnchor,
                owner);
        }

        int[] longestIncreasingSubsequence = moved
            ? GetLongestIncreasingSubsequence(previousIndexByNext)
            : [];
        int sequenceIndex = longestIncreasingSubsequence.Length - 1;
        for (int nextIndex = nextValues.Count - 1; nextIndex >= 0; nextIndex--)
        {
            TNode? anchor = nextIndex + 1 < nextMounted.Length
                ? nextMounted[nextIndex + 1]!.FirstHostNode
                : endAnchor;
            if (nextMounted[nextIndex] is null)
            {
                nextMounted[nextIndex] = Mount(
                    tree,
                    nextValues[nextIndex],
                    container,
                    anchor,
                    owner);
            }
            else if (moved)
            {
                if (sequenceIndex < 0
                    || longestIncreasingSubsequence[sequenceIndex] != nextIndex)
                {
                    Move(nextMounted[nextIndex]!, container, anchor);
                    ReorderTeleportTargetRange(
                        nextMounted[nextIndex]!,
                        nextMounted,
                        nextIndex);
                }
                else
                {
                    sequenceIndex--;
                }
            }
        }

        List<MountedNode<TNode>> result = new(nextValues.Count);
        for (int index = 0; index < nextMounted.Length; index++)
        {
            result.Add(nextMounted[index]!);
        }

        return result;
    }

    private static int FindNextKeylessIndex(
        VirtualNode previous,
        IReadOnlyList<VirtualNode> nextValues,
        IReadOnlyList<bool> claimed)
    {
        for (int index = 0; index < nextValues.Count; index++)
        {
            if (!claimed[index]
                && nextValues[index].Key is null
                && IsSameNodeType(previous, nextValues[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static int[] GetLongestIncreasingSubsequence(IReadOnlyList<int> source)
    {
        int[] predecessors = new int[source.Count];
        int[] result = new int[source.Count];
        int resultLength = 0;
        for (int index = 0; index < source.Count; index++)
        {
            int value = source[index];
            if (value == 0)
            {
                continue;
            }

            int low = 0;
            int high = resultLength;
            while (low < high)
            {
                int middle = (low + high) >> 1;
                if (source[result[middle]] < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            predecessors[index] = low > 0 ? result[low - 1] : -1;
            result[low] = index;
            if (low == resultLength)
            {
                resultLength++;
            }
        }

        int[] sequence = new int[resultLength];
        int cursor = resultLength == 0 ? -1 : result[resultLength - 1];
        for (int index = resultLength - 1; index >= 0; index--)
        {
            sequence[index] = cursor;
            cursor = predecessors[cursor];
        }

        return sequence;
    }

    private bool TryPatchBlockChildren(
        MountedTree<TNode> tree,
        CompositeVirtualNode previous,
        CompositeVirtualNode next,
        TNode fallbackContainer)
    {
        IReadOnlyList<VirtualNode>? previousDynamic = previous.RenderPlan.DynamicChildren;
        IReadOnlyList<VirtualNode>? nextDynamic = next.RenderPlan.DynamicChildren;
        if (previousDynamic is null
            || nextDynamic is null
            || previousDynamic.Count != nextDynamic.Count)
        {
            return false;
        }

        for (int index = 0; index < previousDynamic.Count; index++)
        {
            if (!tree.Nodes.ContainsKey(previousDynamic[index]))
            {
                return false;
            }
        }

        for (int index = 0; index < previousDynamic.Count; index++)
        {
            MountedNode<TNode> current = tree.Nodes[previousDynamic[index]];
            TNode parent = HostParentOrFallback(current.FirstHostNode, fallbackContainer);
            MountedNode<TNode> replacement = Patch(
                tree,
                current,
                nextDynamic[index],
                parent,
                GetNextHostNode(current),
                current.Owner);
            if (!ReferenceEquals(current, replacement) && tree.Root is not null)
            {
                ReplaceMountedNodeReference(tree.Root, current, replacement);
            }
        }

        return true;
    }

    private static bool ReplaceMountedNodeReference(
        MountedNode<TNode> parent,
        MountedNode<TNode> current,
        MountedNode<TNode> replacement)
    {
        switch (parent)
        {
            case MountedComponent<TNode> component:
                if (ReferenceEquals(component.Subtree, current))
                {
                    component.Subtree = replacement;
                    return true;
                }

                return ReplaceMountedNodeReference(component.Subtree, current, replacement);
            case MountedElement<TNode> element:
                return ReplaceMountedNodeReference(element.Children, current, replacement);
            case MountedRange<TNode> range:
                return ReplaceMountedNodeReference(range.Children, current, replacement);
            case MountedTeleport<TNode> teleport:
                return ReplaceMountedNodeReference(teleport.Children, current, replacement);
            case MountedKeepAlive<TNode> keepAlive:
                return keepAlive.ReplaceReference(current, replacement);
            case MountedSuspense<TNode> suspense:
                if (ReferenceEquals(suspense.ActiveBranch, current))
                {
                    suspense.ActiveBranch = replacement;
                    return true;
                }

                return ReplaceMountedNodeReference(
                    suspense.ActiveBranch,
                    current,
                    replacement);
            case MountedTransition<TNode> transition:
                if (ReferenceEquals(transition.Child, current))
                {
                    transition.Child = replacement;
                    return true;
                }

                return ReplaceMountedNodeReference(transition.Child, current, replacement);
            default:
                return false;
        }
    }

    private static bool ReplaceMountedNodeReference(
        List<MountedNode<TNode>> children,
        MountedNode<TNode> current,
        MountedNode<TNode> replacement)
    {
        for (int index = 0; index < children.Count; index++)
        {
            if (ReferenceEquals(children[index], current))
            {
                children[index] = replacement;
                return true;
            }

            if (ReplaceMountedNodeReference(children[index], current, replacement))
            {
                return true;
            }
        }

        return false;
    }

    private static void CarryForwardStaticChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<VirtualNode> previousValues,
        IReadOnlyList<VirtualNode> nextValues,
        IReadOnlyList<MountedNode<TNode>> mountedChildren)
    {
        if (previousValues.Count != nextValues.Count
            || mountedChildren.Count != nextValues.Count)
        {
            return;
        }

        for (int index = 0; index < nextValues.Count; index++)
        {
            VirtualNode previous = previousValues[index];
            VirtualNode next = nextValues[index];
            MountedNode<TNode> mounted = mountedChildren[index];
            if (ReferenceEquals(previous, next) || tree.Nodes.ContainsKey(next))
            {
                continue;
            }

            if (!IsSameNodeType(previous, next))
            {
                continue;
            }

            if (tree.Nodes.TryGetValue(previous, out MountedNode<TNode>? registered)
                && ReferenceEquals(registered, mounted))
            {
                tree.Nodes.Remove(previous);
            }

            mounted.Value = next;
            tree.Nodes[next] = mounted;
            switch (previous, next, mounted)
            {
                case (ElementNode previousElement, ElementNode nextElement,
                    MountedElement<TNode> element):
                    CarryForwardStaticChildren(
                        tree,
                        previousElement.Children,
                        nextElement.Children,
                        element.Children);
                    break;
                case (FragmentNode previousFragment, FragmentNode nextFragment,
                    MountedRange<TNode> fragment):
                    CarryForwardStaticChildren(
                        tree,
                        previousFragment.Children,
                        nextFragment.Children,
                        fragment.Children);
                    break;
                case (TeleportNode previousTeleport, TeleportNode nextTeleport,
                    MountedTeleport<TNode> teleport):
                    CarryForwardStaticChildren(
                        tree,
                        previousTeleport.Children,
                        nextTeleport.Children,
                        teleport.Children);
                    break;
            }
        }
    }

    private List<DirectiveBinding> ResolveDirectiveBindings(
        MountedTree<TNode> tree,
        IReadOnlyList<DirectiveInvocation> invocations,
        RuntimeComponentContext? owner,
        IReadOnlyList<DirectiveBinding>? previousBindings = null)
    {
        if (invocations.Count == 0)
        {
            return [];
        }

        if (tree.Application is null)
        {
            throw new InvalidOperationException(
                "Directive-bearing nodes require an application context and directive resolver.");
        }

        if (tree.Application.Directives is null)
        {
            tree.Application.WarnHandler?.Invoke(
                "Directive-bearing nodes were rendered without an application directive resolver.");
            return [];
        }

        List<DirectiveBinding> resolved = new(invocations.Count);
        bool[] matchedPrevious = new bool[previousBindings?.Count ?? 0];
        for (int invocationIndex = 0; invocationIndex < invocations.Count; invocationIndex++)
        {
            DirectiveInvocation invocation = invocations[invocationIndex];
            IDirective? directive;
            try
            {
                directive = tree.Application.Directives.Resolve(invocation.DirectiveType);
            }
            catch (Exception exception)
            {
                RouteRendererError(
                    tree,
                    owner,
                    exception,
                    "directive resolution");
                continue;
            }

            if (directive is null)
            {
                tree.Application.WarnHandler?.Invoke(
                    $"Directive type '{invocation.DirectiveType}' is not registered.");
                continue;
            }

            object? previousValue = null;
            if (previousBindings is not null)
            {
                for (int previousIndex = 0;
                    previousIndex < previousBindings.Count;
                    previousIndex++)
                {
                    if (!matchedPrevious[previousIndex]
                        && previousBindings[previousIndex].DirectiveType
                            == invocation.DirectiveType)
                    {
                        matchedPrevious[previousIndex] = true;
                        previousValue = previousBindings[previousIndex].Value;
                        break;
                    }
                }
            }

            resolved.Add(
                new DirectiveBinding(
                    invocation.DirectiveType,
                    directive,
                    owner,
                    invocation.Value,
                    previousValue));
        }

        return resolved;
    }

    private static void BindDirectiveHostElements(
        MountedElement<TNode> mounted,
        IReadOnlyList<DirectiveBinding> bindings)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            bindings[index].BindHostElements(
                localName => GetDirectiveHostElements(mounted, localName));
        }
    }

    private void BindActiveTransition(
        MountedTree<TNode> tree,
        TNode element,
        IReadOnlyList<DirectiveBinding> bindings)
    {
        TransitionMountContext<TNode>? active = _activeTransitionMount;
        if (active is null || active.IsClaimed)
        {
            return;
        }

        active.IsClaimed = true;
        active.Element = element;
        for (TransitionMountContext<TNode>? ancestor = active.Parent;
            ancestor is not null && !ancestor.IsClaimed;
            ancestor = ancestor.Parent)
        {
            ancestor.IsClaimed = true;
            ancestor.Element = element;
            ancestor.Suppress();
        }

        ComponentTransition transition = active.Controller.ComponentTransition;
        for (int index = 0; index < bindings.Count; index++)
        {
            bindings[index].BindTransition(transition);
        }

        if (!active.IsSuppressed
            && active.ShouldEnter
            && !active.IsHydrating
            && !active.Controller.Properties.Persisted
            && !active.BeforeEnterInvoked)
        {
            active.BeforeEnterInvoked = active.Controller.BeforeEnter(element);
        }

        _ = tree;
    }

    private static IReadOnlyList<DirectiveHostElement> GetDirectiveHostElements(
        MountedElement<TNode> mounted,
        string localName)
    {
        List<DirectiveHostElement> elements = [];
        for (int index = 0; index < mounted.Children.Count; index++)
        {
            CollectDirectiveHostElements(mounted.Children[index], localName, elements);
        }

        return elements.AsReadOnly();
    }

    private static void CollectDirectiveHostElements(
        MountedNode<TNode> mounted,
        string localName,
        List<DirectiveHostElement> elements)
    {
        switch (mounted)
        {
            case MountedElement<TNode> element:
                ElementNode elementValue = (ElementNode)element.Value;
                if (string.Equals(
                    elementValue.Name.LocalName,
                    localName,
                    StringComparison.Ordinal))
                {
                    elements.Add(new DirectiveHostElement(elementValue, element.HostNode));
                }

                for (int index = 0; index < element.Children.Count; index++)
                {
                    CollectDirectiveHostElements(element.Children[index], localName, elements);
                }

                break;
            case MountedComponent<TNode> component:
                CollectDirectiveHostElements(component.Subtree, localName, elements);
                break;
            case MountedRange<TNode> range:
                for (int index = 0; index < range.Children.Count; index++)
                {
                    CollectDirectiveHostElements(range.Children[index], localName, elements);
                }

                break;
            case MountedTeleport<TNode> teleport:
                for (int index = 0; index < teleport.Children.Count; index++)
                {
                    CollectDirectiveHostElements(teleport.Children[index], localName, elements);
                }

                break;
            case MountedKeepAlive<TNode> keepAlive:
                CollectDirectiveHostElements(keepAlive.Active, localName, elements);
                break;
            case MountedSuspense<TNode> suspense:
                CollectDirectiveHostElements(suspense.ActiveBranch, localName, elements);
                break;
            case MountedTransition<TNode> transition:
                CollectDirectiveHostElements(transition.Child, localName, elements);
                break;
        }
    }

    private static void InvokeDirectiveHooks(
        MountedTree<TNode> tree,
        TNode element,
        IReadOnlyList<DirectiveBinding> bindings,
        ElementNode value,
        ElementNode? previousValue,
        DirectiveHookKind hookKind)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            DirectiveBinding binding = bindings[index];
            DirectiveHook? hook = hookKind switch
            {
                DirectiveHookKind.Created => binding.Directive.Created,
                DirectiveHookKind.BeforeMount => binding.Directive.BeforeMount,
                DirectiveHookKind.Mounted => binding.Directive.Mounted,
                DirectiveHookKind.BeforeUpdate => binding.Directive.BeforeUpdate,
                DirectiveHookKind.Updated => binding.Directive.Updated,
                DirectiveHookKind.BeforeUnmount => binding.Directive.BeforeUnmount,
                DirectiveHookKind.Unmounted => binding.Directive.Unmounted,
                _ => throw new InvalidOperationException(
                    $"Unknown directive hook kind: {hookKind}."),
            };
            if (hook is null)
            {
                continue;
            }

            try
            {
                if (binding.Context is RuntimeComponentContext context)
                {
                    context.Run(() => hook(element, binding, value, previousValue));
                }
                else
                {
                    hook(element, binding, value, previousValue);
                }
            }
            catch (Exception exception)
            {
                RouteRendererError(
                    tree,
                    binding.Context as RuntimeComponentContext,
                    exception,
                    $"{hookKind} directive lifecycle hook");
            }
        }
    }

    private void QueueDirectiveHooks(
        MountedTree<TNode> tree,
        MountedElement<TNode> mounted,
        ElementNode value,
        ElementNode? previousValue,
        DirectiveHookKind hookKind,
        bool invokeAfterUnmount = false)
    {
        if (mounted.DirectiveBindings.Count == 0)
        {
            return;
        }

        List<DirectiveBinding> bindings = mounted.DirectiveBindings;
        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    if (!invokeAfterUnmount && mounted.IsUnmounted)
                    {
                        return;
                    }

                    InvokeDirectiveHooks(
                        tree,
                        mounted.HostNode,
                        bindings,
                        value,
                        previousValue,
                        hookKind);
                    QueueHostCommit();
                })
            {
                Name = $"directive {hookKind} lifecycle",
            });
    }

    private static bool HasVirtualNodeLifecycleHook(ElementNode value, string name)
    {
        for (int index = 0; index < value.Bindings.Count; index++)
        {
            ElementBinding binding = value.Bindings[index];
            if (string.Equals(binding.Name.LocalName, name, StringComparison.Ordinal)
                && binding.Value is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static VirtualNodeLifecycleHook? GetVirtualNodeLifecycleHook(
        ElementNode value,
        string name)
    {
        for (int index = 0; index < value.Bindings.Count; index++)
        {
            ElementBinding binding = value.Bindings[index];
            if (!string.Equals(binding.Name.LocalName, name, StringComparison.Ordinal))
            {
                continue;
            }

            return binding.Value switch
            {
                null => null,
                VirtualNodeLifecycleHook hook => hook,
                Action<VirtualNode, VirtualNode?> action =>
                    new VirtualNodeLifecycleHook(action),
                Action<ElementNode, ElementNode?> action =>
                    (current, previous) => action(
                        (ElementNode)current,
                        (ElementNode?)previous),
                _ => throw new NotSupportedException(
                    $"Virtual-node lifecycle binding '{name}' must contain a "
                        + $"{nameof(VirtualNodeLifecycleHook)}."),
            };
        }

        return null;
    }

    private static void InvokeVirtualNodeLifecycleHook(
        MountedTree<TNode> tree,
        RuntimeComponentContext? owner,
        ElementNode value,
        ElementNode? previousValue,
        string name)
    {
        try
        {
            VirtualNodeLifecycleHook? hook = GetVirtualNodeLifecycleHook(value, name);
            if (hook is null)
            {
                return;
            }

            if (owner is null)
            {
                hook(value, previousValue);
            }
            else
            {
                owner.Run(() => hook(value, previousValue));
            }
        }
        catch (Exception exception)
        {
            RouteRendererError(
                tree,
                owner,
                exception,
                $"virtual-node lifecycle hook '{name}'");
        }
    }

    private static void QueueVirtualNodeLifecycleHook(
        MountedTree<TNode> tree,
        RuntimeComponentContext? owner,
        MountedNode<TNode>? mounted,
        ElementNode value,
        ElementNode? previousValue,
        string name)
    {
        if (!HasVirtualNodeLifecycleHook(value, name))
        {
            return;
        }

        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    if (mounted is null || !mounted.IsUnmounted)
                    {
                        InvokeVirtualNodeLifecycleHook(
                            tree,
                            owner,
                            value,
                            previousValue,
                            name);
                    }
                })
            {
                Name = $"virtual node {name} lifecycle",
            });
    }

    private static void RouteRendererError(
        MountedTree<TNode> tree,
        RuntimeComponentContext? owner,
        Exception exception,
        string diagnosticInformation)
    {
        if (owner is not null)
        {
            owner.RouteError(exception, diagnosticInformation);
        }
        else if (tree.Application?.ErrorHandler is { } errorHandler)
        {
            errorHandler(exception, null, diagnosticInformation);
        }
        else
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private void MountAttributes(TNode element, IReadOnlyList<ElementBinding> bindings)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            ElementBinding binding = bindings[index];
            if (!IsValueBinding(binding) && !IsNodeLifecycleBinding(binding))
            {
                _options.PatchAttribute(element, null, binding);
            }
        }

        for (int index = 0; index < bindings.Count; index++)
        {
            ElementBinding binding = bindings[index];
            if (IsValueBinding(binding))
            {
                _options.PatchAttribute(element, null, binding);
            }
        }
    }

    private void PatchElementAttributes(
        TNode element,
        ElementNode previous,
        ElementNode next)
    {
        PatchFlags flags = next.RenderPlan.PatchFlags;
        if ((int)flags <= 0)
        {
            return;
        }

        if ((flags & PatchFlags.FullProps) != 0)
        {
            PatchAttributes(element, previous.Bindings, next.Bindings);
            return;
        }

        if ((flags & PatchFlags.Class) != 0)
        {
            PatchNamedBinding(element, previous.Bindings, next.Bindings, "class");
        }

        if ((flags & PatchFlags.Style) != 0)
        {
            PatchNamedBinding(element, previous.Bindings, next.Bindings, "style");
        }

        if ((flags & PatchFlags.Props) != 0)
        {
            IReadOnlyList<int>? indices = next.RenderPlan.DynamicBindingIndices;
            if (indices is null)
            {
                PatchAttributes(element, previous.Bindings, next.Bindings);
                return;
            }

            for (int index = 0; index < indices.Count; index++)
            {
                int bindingIndex = indices[index];
                ElementBinding? previousBinding = bindingIndex < previous.Bindings.Count
                    ? previous.Bindings[bindingIndex]
                    : null;
                ElementBinding? nextBinding = bindingIndex < next.Bindings.Count
                    ? next.Bindings[bindingIndex]
                    : null;
                PatchBindingDifference(element, previousBinding, nextBinding);
            }
        }
    }

    private void PatchAttributes(
        TNode element,
        IReadOnlyList<ElementBinding> previous,
        IReadOnlyList<ElementBinding> next)
    {
        bool[] matchedPrevious = new bool[previous.Count];
        for (int nextIndex = 0; nextIndex < next.Count; nextIndex++)
        {
            ElementBinding nextBinding = next[nextIndex];
            int previousIndex = FindBinding(previous, nextBinding);
            ElementBinding? previousBinding = previousIndex >= 0
                ? previous[previousIndex]
                : null;
            if (previousIndex >= 0)
            {
                matchedPrevious[previousIndex] = true;
            }

            PatchBindingDifference(element, previousBinding, nextBinding);
        }

        for (int previousIndex = 0; previousIndex < previous.Count; previousIndex++)
        {
            if (!matchedPrevious[previousIndex])
            {
                ElementBinding previousBinding = previous[previousIndex];
                if (!IsNodeLifecycleBinding(previousBinding))
                {
                    _options.PatchAttribute(element, previousBinding, null);
                }
            }
        }
    }

    private void PatchNamedBinding(
        TNode element,
        IReadOnlyList<ElementBinding> previous,
        IReadOnlyList<ElementBinding> next,
        string localName)
    {
        ElementBinding? previousBinding = FindBinding(previous, localName);
        ElementBinding? nextBinding = FindBinding(next, localName);
        PatchBindingDifference(element, previousBinding, nextBinding);
    }

    private void PatchBindingDifference(
        TNode element,
        ElementBinding? previous,
        ElementBinding? next)
    {
        if ((previous is not null && IsNodeLifecycleBinding(previous))
            || (next is not null && IsNodeLifecycleBinding(next)))
        {
            return;
        }

        if (previous is not null
            && next is not null
            && previous.Kind == next.Kind
            && previous.Name == next.Name
            && !IsValueBinding(next)
            && Equals(previous.Value, next.Value))
        {
            return;
        }

        if (previous is not null
            && next is not null
            && (previous.Kind != next.Kind || previous.Name != next.Name))
        {
            _options.PatchAttribute(element, previous, null);
            _options.PatchAttribute(element, null, next);
            return;
        }

        if (previous is not null || next is not null)
        {
            _options.PatchAttribute(element, previous, next);
        }
    }

    private static bool IsValueBinding(ElementBinding binding) =>
        string.Equals(binding.Name.LocalName, "value", StringComparison.Ordinal);

    private static bool IsNodeLifecycleBinding(ElementBinding binding) =>
        binding.Name.LocalName.StartsWith("onVnode", StringComparison.Ordinal);

    private static int FindBinding(
        IReadOnlyList<ElementBinding> bindings,
        ElementBinding value)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            if (bindings[index].Kind == value.Kind
                && bindings[index].Name == value.Name)
            {
                return index;
            }
        }

        return -1;
    }

    private static ElementBinding? FindBinding(
        IReadOnlyList<ElementBinding> bindings,
        string localName)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            if (string.Equals(
                bindings[index].Name.LocalName,
                localName,
                StringComparison.Ordinal))
            {
                return bindings[index];
            }
        }

        return null;
    }

    private void Move(MountedNode<TNode> mounted, TNode container, TNode? anchor)
    {
        switch (mounted)
        {
            case MountedComponent<TNode> component:
                Move(component.Subtree, container, anchor);
                break;
            case MountedElement<TNode> element:
                _options.Insert(element.HostNode, container, anchor);
                break;
            case MountedLeaf<TNode> leaf:
                _options.Insert(leaf.HostNode, container, anchor);
                break;
            case MountedStatic<TNode> staticContent:
                MoveRange(staticContent.First, staticContent.Last, container, anchor);
                break;
            case MountedRange<TNode> range:
                MoveRange(range.StartAnchor, range.EndAnchor, container, anchor);
                break;
            case MountedTeleport<TNode> teleport:
                MoveRange(teleport.StartAnchor, teleport.EndAnchor, container, anchor);
                break;
            case MountedKeepAlive<TNode> keepAlive:
                MoveRange(keepAlive.StartAnchor, keepAlive.EndAnchor, container, anchor);
                break;
            case MountedSuspense<TNode> suspense:
                MoveRange(suspense.StartAnchor, suspense.EndAnchor, container, anchor);
                break;
            case MountedTransition<TNode> transition:
                Move(transition.Child, container, anchor);
                break;
        }
    }

    private void MoveRange(TNode first, TNode last, TNode container, TNode? anchor)
    {
        TNode current = first;
        while (true)
        {
            bool isLast = NodeComparer.Equals(current, last);
            TNode? next = isLast ? default : _options.NextSibling(current);
            _options.Insert(current, container, anchor);
            if (isLast)
            {
                break;
            }

            current = RequireHostNode(next, "range sibling");
        }
    }

    private void Unmount(
        MountedTree<TNode> tree,
        MountedNode<TNode> mounted,
        bool removeHostNodes)
    {
        if (mounted.IsUnmounted)
        {
            return;
        }

        UnmountVisitCount++;
        ClearReference(tree, mounted);
        switch (mounted)
        {
            case MountedComponent<TNode> component:
                UnmountComponent(tree, component, removeHostNodes);
                break;
            case MountedElement<TNode> element:
                ElementNode elementValue = (ElementNode)element.Value;
                InvokeVirtualNodeLifecycleHook(
                    tree,
                    element.Owner,
                    elementValue,
                    previousValue: null,
                    "onVnodeBeforeUnmount");
                InvokeDirectiveHooks(
                    tree,
                    element.HostNode,
                    element.DirectiveBindings,
                    elementValue,
                    previousValue: null,
                    DirectiveHookKind.BeforeUnmount);
                UnmountOwnedChildren(
                    tree,
                    element,
                    element.Children,
                    elementValue.RenderPlan,
                    removeChildrenHostNodes: false);
                if (removeHostNodes)
                {
                    _options.Remove(element.HostNode);
                }

                QueueVirtualNodeLifecycleHook(
                    tree,
                    element.Owner,
                    mounted: null,
                    elementValue,
                    previousValue: null,
                    "onVnodeUnmounted");
                QueueDirectiveHooks(
                    tree,
                    element,
                    elementValue,
                    previousValue: null,
                    DirectiveHookKind.Unmounted,
                    invokeAfterUnmount: true);

                break;
            case MountedLeaf<TNode> leaf:
                if (removeHostNodes)
                {
                    _options.Remove(leaf.HostNode);
                }

                break;
            case MountedStatic<TNode> staticContent:
                if (removeHostNodes)
                {
                    RemoveRange(staticContent.First, staticContent.Last);
                }

                break;
            case MountedRange<TNode> range:
                UnmountOwnedChildren(
                    tree,
                    range,
                    range.Children,
                    ((FragmentNode)range.Value).RenderPlan,
                    removeChildrenHostNodes: false);
                if (removeHostNodes)
                {
                    RemoveRange(range.StartAnchor, range.EndAnchor);
                }

                break;
            case MountedTeleport<TNode> teleport:
                UnmountTeleport(tree, teleport, removeHostNodes);
                break;
            case MountedKeepAlive<TNode> keepAlive:
                UnmountKeepAlive(tree, keepAlive, removeHostNodes);
                break;
            case MountedSuspense<TNode> suspense:
                UnmountSuspense(tree, suspense, removeHostNodes);
                break;
            case MountedTransition<TNode> transition:
                UnmountTransition(tree, transition, removeHostNodes);
                break;
        }

        Unregister(tree, mounted);
        mounted.IsUnmounted = true;
    }

    private void Remove(
        MountedTree<TNode> tree,
        MountedNode<TNode> mounted)
    {
        if (mounted.IsUnmounted)
        {
            return;
        }

        if (mounted is MountedTransition<TNode> transition
            && TryDeferTransitionUnmount(tree, transition))
        {
            UnmountVisitCount++;
            return;
        }

        Unmount(tree, mounted, removeHostNodes: true);
    }

    private void UnmountOwnedChildren(
        MountedTree<TNode> tree,
        MountedNode<TNode> parent,
        IReadOnlyList<MountedNode<TNode>> children,
        RenderPlan plan,
        bool removeChildrenHostNodes)
    {
        if (!TryUnmountBlockChildren(
            tree,
            parent,
            children,
            plan,
            removeChildrenHostNodes))
        {
            UnmountChildren(tree, children, removeChildrenHostNodes);
        }
    }

    private bool TryUnmountBlockChildren(
        MountedTree<TNode> tree,
        MountedNode<TNode> parent,
        IReadOnlyList<MountedNode<TNode>> children,
        RenderPlan plan,
        bool removeHostNodes)
    {
        if (plan.PatchFlags == PatchFlags.Bail
            || (int)plan.PatchFlags <= 0
            || plan.DynamicChildren is null
            || (parent.Value is FragmentNode
                && (plan.PatchFlags & PatchFlags.StableFragment) == 0))
        {
            return false;
        }

        HashSet<MountedNode<TNode>> visited = new(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < plan.DynamicChildren.Count; index++)
        {
            if (tree.Nodes.TryGetValue(
                plan.DynamicChildren[index],
                out MountedNode<TNode>? dynamicMounted)
                && visited.Add(dynamicMounted))
            {
                Unmount(tree, dynamicMounted, removeHostNodes);
            }
        }

        ReleaseSkippedChildren(tree, children, visited);
        return true;
    }

    private void ReleaseSkippedChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<MountedNode<TNode>> children,
        HashSet<MountedNode<TNode>> visited)
    {
        for (int index = 0; index < children.Count; index++)
        {
            MountedNode<TNode> child = children[index];
            if (visited.Contains(child))
            {
                continue;
            }

            if (RequiresUnmountVisit(child))
            {
                Unmount(tree, child, removeHostNodes: false);
            }
            else
            {
                ReleaseSkippedNode(tree, child, visited);
            }
        }
    }

    private void ReleaseSkippedNode(
        MountedTree<TNode> tree,
        MountedNode<TNode> mounted,
        HashSet<MountedNode<TNode>> visited)
    {
        if (visited.Contains(mounted))
        {
            return;
        }

        ClearReference(tree, mounted);
        switch (mounted)
        {
            case MountedElement<TNode> element:
                for (int index = 0; index < element.Children.Count; index++)
                {
                    ReleaseOrUnmountSkippedNode(tree, element.Children[index], visited);
                }

                break;
            case MountedRange<TNode> range:
                for (int index = 0; index < range.Children.Count; index++)
                {
                    ReleaseOrUnmountSkippedNode(tree, range.Children[index], visited);
                }

                break;
        }

        Unregister(tree, mounted);
        mounted.IsUnmounted = true;
    }

    private void ReleaseOrUnmountSkippedNode(
        MountedTree<TNode> tree,
        MountedNode<TNode> mounted,
        HashSet<MountedNode<TNode>> visited)
    {
        if (visited.Contains(mounted))
        {
            return;
        }

        if (RequiresUnmountVisit(mounted))
        {
            Unmount(tree, mounted, removeHostNodes: false);
        }
        else
        {
            ReleaseSkippedNode(tree, mounted, visited);
        }
    }

    private static bool RequiresUnmountVisit(MountedNode<TNode> mounted)
    {
        if (mounted.Value.MountReference is not null)
        {
            return true;
        }

        return mounted is MountedComponent<TNode>
            or MountedTeleport<TNode>
            or MountedKeepAlive<TNode>
            or MountedSuspense<TNode>
            or MountedTransition<TNode>
            || mounted is MountedElement<TNode> element
                && (element.DirectiveBindings.Count > 0
                    || HasVirtualNodeLifecycleHook(
                        (ElementNode)element.Value,
                        "onVnodeBeforeUnmount")
                    || HasVirtualNodeLifecycleHook(
                        (ElementNode)element.Value,
                        "onVnodeUnmounted"));
    }

    private void UnmountChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<MountedNode<TNode>> children,
        bool removeHostNodes)
    {
        for (int index = 0; index < children.Count; index++)
        {
            Unmount(tree, children[index], removeHostNodes);
        }
    }

    private void RemoveRange(TNode first, TNode last)
    {
        TNode current = first;
        while (true)
        {
            bool isLast = NodeComparer.Equals(current, last);
            TNode? next = isLast ? default : _options.NextSibling(current);
            _options.Remove(current);
            if (isLast)
            {
                break;
            }

            current = RequireHostNode(next, "range sibling");
        }
    }

    private TNode? GetNextHostNode(MountedNode<TNode> mounted) =>
        _options.NextSibling(mounted.LastHostNode);

    private TNode HostParentOrFallback(TNode node, TNode fallback) =>
        HasHostNode(_options.ParentNode(node)) ? _options.ParentNode(node)! : fallback;

    private void QueueHostCommit() => Scheduler.QueueHostCommit(_options.Commit);

    private void UpdateReference(
        MountedTree<TNode> tree,
        MountedNode<TNode> mounted,
        MountReference? previous,
        MountReference? next)
    {
        if (mounted is MountedComponent<TNode>
            {
                Instance: AsynchronousComponentWrapper,
            })
        {
            return;
        }

        if (ReferenceEquals(previous, next))
        {
            return;
        }

        if (previous is not null)
        {
            InvokeReference(tree, mounted.Owner, previous, null);
        }

        if (next is not null)
        {
            InvokeReference(tree, mounted.Owner, next, ReferenceValue(mounted));
        }
    }

    private void ClearReference(MountedTree<TNode> tree, MountedNode<TNode> mounted)
    {
        if (mounted is MountedComponent<TNode>
            {
                Instance: AsynchronousComponentWrapper,
            })
        {
            return;
        }

        if (mounted.Value.MountReference is { } reference)
        {
            InvokeReference(tree, mounted.Owner, reference, null);
        }
    }

    private static void InvokeReference(
        MountedTree<TNode> tree,
        RuntimeComponentContext? owner,
        MountReference reference,
        object? value)
    {
        try
        {
            if (owner is null)
            {
                reference(value);
            }
            else
            {
                owner.Run(() => reference(value));
            }
        }
        catch (Exception exception)
        {
            if (owner is not null)
            {
                owner.RouteError(exception, "mount-reference callback");
            }
            else if (tree.Application?.ErrorHandler is { } errorHandler)
            {
                errorHandler(exception, null, "mount-reference callback");
            }
            else
            {
                throw;
            }
        }
    }

    private static object? ReferenceValue(MountedNode<TNode> mounted)
    {
        return mounted switch
        {
            MountedComponent<TNode> component when component.Context.HasExposedValue =>
                component.Context.ExposedValue,
            MountedComponent<TNode> component => component.Instance,
            MountedElement<TNode> element => element.HostNode,
            MountedLeaf<TNode> leaf => leaf.HostNode,
            _ => mounted.FirstHostNode,
        };
    }

    private static bool IsSameNodeType(VirtualNode current, VirtualNode next)
    {
        if (current.GetType() != next.GetType() || !Equals(current.Key, next.Key))
        {
            return false;
        }

        return (current, next) switch
        {
            (ElementNode currentElement, ElementNode nextElement) =>
                currentElement.Name == nextElement.Name,
            (ComponentNode currentComponent, ComponentNode nextComponent) =>
                Equals(currentComponent.Component, nextComponent.Component),
            (StaticNode currentStatic, StaticNode nextStatic) =>
                currentStatic.Format == nextStatic.Format
                && string.Equals(
                    currentStatic.Content,
                    nextStatic.Content,
                    StringComparison.Ordinal),
            _ => true,
        };
    }

    private static void Register(
        MountedTree<TNode> tree,
        VirtualNode value,
        MountedNode<TNode> mounted)
    {
        tree.Nodes.Add(value, mounted);
    }

    private static void ReplaceValue(
        MountedTree<TNode> tree,
        MountedNode<TNode> mounted,
        VirtualNode next)
    {
        VirtualNode previous = mounted.Value;
        if (tree.Nodes.TryGetValue(previous, out MountedNode<TNode>? registered)
            && ReferenceEquals(registered, mounted))
        {
            tree.Nodes.Remove(previous);
        }

        mounted.Value = next;
        tree.Nodes[next] = mounted;
    }

    private static void Unregister(MountedTree<TNode> tree, MountedNode<TNode> mounted)
    {
        if (tree.Nodes.TryGetValue(mounted.Value, out MountedNode<TNode>? registered)
            && ReferenceEquals(registered, mounted))
        {
            tree.Nodes.Remove(mounted.Value);
        }
    }

    private static bool HasHostNode(TNode? node) =>
        node is not null && !NodeComparer.Equals(node, default!);

    private static TNode RequireHostNode(TNode? node, string parameterName)
    {
        if (!HasHostNode(node))
        {
            throw new ArgumentException(
                "The default host-node value is reserved for no node.",
                parameterName);
        }

        return node!;
    }
}
