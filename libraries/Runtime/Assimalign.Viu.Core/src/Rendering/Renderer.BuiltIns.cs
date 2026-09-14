using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private static readonly IReadOnlyDictionary<string, object?> EmptySlotArguments =
        new Dictionary<string, object?>(StringComparer.Ordinal);
    private static readonly QualifiedName StorageContainerName = new(
        "storage",
        "urn:assimalign:viu:internal");

    private SuspenseBoundary? _activeSuspenseBoundary;

    private MountedTeleport<TNode> MountTeleport(
        MountedTree<TNode> tree,
        TeleportNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        TNode startAnchor = _options.CreateComment(HydrationMarkers.TeleportStartData);
        TNode endAnchor = _options.CreateComment(HydrationMarkers.TeleportEndData);
        _options.Insert(startAnchor, container, anchor);
        _options.Insert(endAnchor, container, anchor);
        MountedTeleport<TNode> mounted = new(
            value,
            startAnchor,
            endAnchor,
            default,
            default,
            hasTarget: false,
            [],
            owner);
        if (value.IsDisabled)
        {
            mounted.Children = MountChildren(
                tree,
                value.Children,
                container,
                endAnchor,
                owner);
            mounted.ChildrenMounted = true;
            if (value.IsDeferred)
            {
                QueueDeferredTeleportMount(tree, mounted, container);
            }
            else
            {
                TryInstallTeleportTargetAnchor(tree, mounted, value);
            }
        }
        else if (value.IsDeferred)
        {
            QueueDeferredTeleportMount(tree, mounted, container);
        }
        else if (TryInstallTeleportTargetAnchor(tree, mounted, value))
        {
            mounted.Children = MountChildren(
                tree,
                value.Children,
                mounted.TargetContainer!,
                mounted.TargetAnchor,
                owner);
            mounted.ChildrenMounted = true;
        }

        Register(tree, value, mounted);
        return mounted;
    }

    private void PatchTeleport(
        MountedTree<TNode> tree,
        MountedTeleport<TNode> mounted,
        TeleportNode next,
        TNode originContainer)
    {
        TeleportNode previous = (TeleportNode)mounted.Value;
        CancelDeferredTeleport(mounted);
        bool canReuseDeferredTarget = next.IsDeferred
            && mounted.HasTarget
            && string.Equals(
                previous.TargetIdentifier,
                next.TargetIdentifier,
                StringComparison.Ordinal);

        if (next.IsDisabled)
        {
            if (mounted.ChildrenMounted)
            {
                MoveChildren(mounted.Children, originContainer, mounted.EndAnchor);
                PatchTeleportChildren(
                    tree,
                    mounted,
                    previous.Children,
                    next.Children,
                    originContainer,
                    mounted.EndAnchor,
                    previous,
                    next);
            }
            else
            {
                mounted.Children = MountChildren(
                    tree,
                    next.Children,
                    originContainer,
                    mounted.EndAnchor,
                    mounted.Owner);
                mounted.ChildrenMounted = true;
            }

            if (next.IsDeferred)
            {
                if (!canReuseDeferredTarget)
                {
                    QueueDeferredTeleportMount(tree, mounted, originContainer);
                }
            }
            else
            {
                EnsureTeleportTarget(
                    tree,
                    mounted,
                    next,
                    moveChildrenToTarget: false);
            }

            ReplaceValue(tree, mounted, next);
            return;
        }

        if (next.IsDeferred && !canReuseDeferredTarget)
        {
            if (mounted.ChildrenMounted)
            {
                TNode currentContainer = previous.IsDisabled
                    ? originContainer
                    : mounted.TargetContainer!;
                TNode? currentAnchor = previous.IsDisabled
                    ? mounted.EndAnchor
                    : mounted.TargetAnchor;
                PatchTeleportChildren(
                    tree,
                    mounted,
                    previous.Children,
                    next.Children,
                    currentContainer,
                    currentAnchor,
                    previous,
                    next);
            }

            QueueDeferredTeleportMount(tree, mounted, originContainer);
            ReplaceValue(tree, mounted, next);
            return;
        }

        if (!canReuseDeferredTarget
            && !EnsureTeleportTarget(
                tree,
                mounted,
                next,
                moveChildrenToTarget: false))
        {
            if (mounted.ChildrenMounted)
            {
                UnmountChildren(tree, mounted.Children, removeHostNodes: true);
                mounted.Children.Clear();
                mounted.ChildrenMounted = false;
            }

            ReplaceValue(tree, mounted, next);
            return;
        }

        if (mounted.ChildrenMounted)
        {
            MoveChildren(
                mounted.Children,
                mounted.TargetContainer!,
                mounted.TargetAnchor);
            PatchTeleportChildren(
                tree,
                mounted,
                previous.Children,
                next.Children,
                mounted.TargetContainer!,
                mounted.TargetAnchor,
                previous,
                next);
        }
        else
        {
            mounted.Children = MountChildren(
                tree,
                next.Children,
                mounted.TargetContainer!,
                mounted.TargetAnchor,
                mounted.Owner);
            mounted.ChildrenMounted = true;
        }

        ReplaceValue(tree, mounted, next);
    }

    private void PatchTeleportChildren(
        MountedTree<TNode> tree,
        MountedTeleport<TNode> mounted,
        IReadOnlyList<VirtualNode> previousChildren,
        IReadOnlyList<VirtualNode> nextChildren,
        TNode container,
        TNode? anchor,
        TeleportNode previous,
        TeleportNode next)
    {
        PatchFlags flags = next.RenderPlan.PatchFlags;
        bool blockPatched = flags != PatchFlags.Bail
            && flags != PatchFlags.Cached
            && TryPatchBlockChildren(tree, mounted, previous, next, container);
        if (flags != PatchFlags.Cached && !blockPatched)
        {
            mounted.Children = PatchChildren(
                tree,
                mounted.Children,
                previousChildren,
                nextChildren,
                container,
                anchor,
                mounted.Owner,
                flags);
        }
        else if (blockPatched)
        {
            CarryForwardStaticChildren(
                previousChildren,
                nextChildren,
                mounted.Children);
        }
    }

    private void QueueDeferredTeleportMount(
        MountedTree<TNode> tree,
        MountedTeleport<TNode> mounted,
        TNode originContainer)
    {
        SchedulerJob job = null!;
        job = new SchedulerJob(
            () =>
            {
                if (mounted.IsUnmounted
                    || !ReferenceEquals(mounted.PendingJob, job)
                    || mounted.Value is not TeleportNode current
                    || !current.IsDeferred)
                {
                    return;
                }

                mounted.PendingJob = null;
                if (!EnsureTeleportTarget(
                    tree,
                    mounted,
                    current,
                    moveChildrenToTarget: !current.IsDisabled))
                {
                    if (!current.IsDisabled && mounted.ChildrenMounted)
                    {
                        UnmountChildren(tree, mounted.Children, removeHostNodes: true);
                        mounted.Children.Clear();
                        mounted.ChildrenMounted = false;
                        RefreshBlockChildren(mounted);
                    }

                    return;
                }

                if (!current.IsDisabled && !mounted.ChildrenMounted)
                {
                    mounted.Children = MountChildren(
                        tree,
                        current.Children,
                        mounted.TargetContainer!,
                        mounted.TargetAnchor,
                        mounted.Owner);
                    mounted.ChildrenMounted = true;
                    RefreshBlockChildren(mounted);
                }

                NormalizeTeleportTargetOrder(tree);
                QueueHostCommit();
            })
        {
            Name = "deferred teleport target setup",
        };
        mounted.PendingJob = job;
        Scheduler.QueuePostFlushCallback(job);
    }

    private bool EnsureTeleportTarget(
        MountedTree<TNode> tree,
        MountedTeleport<TNode> mounted,
        TeleportNode value,
        bool moveChildrenToTarget = true)
    {
        TNode? resolved = _options.ResolveTeleportTarget is { } resolveTeleportTarget
            ? resolveTeleportTarget(value.TargetIdentifier)
            : default;
        if (!HasHostNode(resolved))
        {
            tree.Application?.WarnHandler?.Invoke(
                $"Failed to resolve teleport target '{value.TargetIdentifier}'.");
            RemoveTeleportTargetAnchor(mounted);
            return false;
        }

        if (mounted.HasTarget
            && HasHostNode(mounted.TargetContainer)
            && NodeComparer.Equals(mounted.TargetContainer!, resolved!))
        {
            return true;
        }

        RemoveTeleportTargetAnchor(mounted);
        TNode targetAnchor = _options.CreateComment(HydrationMarkers.TeleportAnchorData);
        _options.Insert(targetAnchor, resolved!, default);
        mounted.TargetContainer = resolved;
        mounted.TargetAnchor = targetAnchor;
        mounted.HasTarget = true;
        if (moveChildrenToTarget && mounted.ChildrenMounted)
        {
            MoveChildren(mounted.Children, resolved!, targetAnchor);
        }

        return true;
    }

    private bool TryInstallTeleportTargetAnchor(
        MountedTree<TNode> tree,
        MountedTeleport<TNode> mounted,
        TeleportNode value) =>
        EnsureTeleportTarget(
            tree,
            mounted,
            value,
            moveChildrenToTarget: !value.IsDisabled);

    private void RemoveTeleportTargetAnchor(MountedTeleport<TNode> mounted)
    {
        if (mounted.HasTarget && HasHostNode(mounted.TargetAnchor))
        {
            _options.Remove(mounted.TargetAnchor!);
        }

        mounted.TargetAnchor = default;
        mounted.TargetContainer = default;
        mounted.HasTarget = false;
    }

    private static void CancelDeferredTeleport(MountedTeleport<TNode> mounted)
    {
        if (mounted.PendingJob is null)
        {
            return;
        }

        mounted.PendingJob.IsDisposed = true;
        Scheduler.InvalidateJob(mounted.PendingJob);
        mounted.PendingJob = null;
    }

    private void UnmountTeleport(
        MountedTree<TNode> tree,
        MountedTeleport<TNode> mounted,
        bool removeHostNodes)
    {
        CancelDeferredTeleport(mounted);
        if (mounted.ChildrenMounted)
        {
            UnmountChildren(tree, mounted.Children, removeHostNodes: true);
        }

        RemoveTeleportTargetAnchor(mounted);
        if (removeHostNodes)
        {
            RemoveRange(mounted.StartAnchor, mounted.EndAnchor);
        }
    }

    private void ReorderTeleportTargetRange(
        MountedNode<TNode> mounted,
        IReadOnlyList<MountedNode<TNode>?> siblings,
        int siblingIndex)
    {
        if (mounted is not MountedTeleport<TNode>
            {
                HasTarget: true,
                TargetContainer: { } targetContainer,
                TargetAnchor: { } targetRangeEnd,
            } teleport)
        {
            return;
        }

        TNode? targetInsertionAnchor = default;
        for (int index = siblingIndex + 1; index < siblings.Count; index++)
        {
            if (siblings[index] is MountedTeleport<TNode>
                {
                    HasTarget: true,
                    TargetContainer: { } candidateTarget,
                } candidate
                && NodeComparer.Equals(targetContainer, candidateTarget))
            {
                targetInsertionAnchor = FirstTeleportTargetHostNode(candidate);
                break;
            }
        }

        MoveRange(
            FirstTeleportTargetHostNode(teleport),
            targetRangeEnd,
            targetContainer,
            targetInsertionAnchor);
    }

    private static TNode FirstTeleportTargetHostNode(MountedTeleport<TNode> teleport)
    {
        if (teleport.Value is TeleportNode { IsDisabled: false }
            && teleport.ChildrenMounted
            && teleport.Children.Count > 0)
        {
            return teleport.Children[0].FirstHostNode;
        }

        return teleport.TargetAnchor!;
    }

    private void NormalizeTeleportTargetOrder(MountedTree<TNode> tree)
    {
        if (tree.Root is null)
        {
            return;
        }

        Dictionary<TNode, List<MountedTeleport<TNode>>> teleportsByTarget =
            new(NodeComparer);
        CollectTargetedTeleports(tree.Root, teleportsByTarget);
        foreach (List<MountedTeleport<TNode>> teleports in teleportsByTarget.Values)
        {
            for (int index = teleports.Count - 2; index >= 0; index--)
            {
                MountedTeleport<TNode> current = teleports[index];
                MountedTeleport<TNode> next = teleports[index + 1];
                TNode insertionAnchor = FirstTeleportTargetHostNode(next);
                TNode? currentNext = _options.NextSibling(current.TargetAnchor!);
                if (HasHostNode(currentNext)
                    && NodeComparer.Equals(currentNext!, insertionAnchor))
                {
                    continue;
                }

                MoveRange(
                    FirstTeleportTargetHostNode(current),
                    current.TargetAnchor!,
                    current.TargetContainer!,
                    insertionAnchor);
            }
        }
    }

    private static void CollectTargetedTeleports(
        MountedNode<TNode> mounted,
        Dictionary<TNode, List<MountedTeleport<TNode>>> teleportsByTarget)
    {
        switch (mounted)
        {
            case MountedComponent<TNode> component:
                CollectTargetedTeleports(component.Subtree, teleportsByTarget);
                break;
            case MountedElement<TNode> element:
                CollectTargetedTeleports(element.Children, teleportsByTarget);
                break;
            case MountedRange<TNode> range:
                CollectTargetedTeleports(range.Children, teleportsByTarget);
                break;
            case MountedTeleport<TNode> teleport:
                if (teleport.HasTarget
                    && HasHostNode(teleport.TargetContainer)
                    && HasHostNode(teleport.TargetAnchor))
                {
                    TNode target = teleport.TargetContainer!;
                    if (!teleportsByTarget.TryGetValue(
                        target,
                        out List<MountedTeleport<TNode>>? teleports))
                    {
                        teleports = [];
                        teleportsByTarget.Add(target, teleports);
                    }

                    teleports.Add(teleport);
                }

                CollectTargetedTeleports(teleport.Children, teleportsByTarget);
                break;
            case MountedKeepAlive<TNode> keepAlive:
                CollectTargetedTeleports(keepAlive.Active, teleportsByTarget);
                break;
            case MountedSuspense<TNode> suspense:
                CollectTargetedTeleports(suspense.ActiveBranch, teleportsByTarget);
                break;
            case MountedTransition<TNode> transition:
                CollectTargetedTeleports(
                    CurrentTransitionChild(transition),
                    teleportsByTarget);
                break;
        }
    }

    private static void CollectTargetedTeleports(
        IReadOnlyList<MountedNode<TNode>> mounted,
        Dictionary<TNode, List<MountedTeleport<TNode>>> teleportsByTarget)
    {
        for (int index = 0; index < mounted.Count; index++)
        {
            CollectTargetedTeleports(mounted[index], teleportsByTarget);
        }
    }

    private MountedKeepAlive<TNode> MountKeepAlive(
        MountedTree<TNode> tree,
        KeepAliveNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        TNode startAnchor = _options.CreateComment("keep-alive start");
        TNode endAnchor = _options.CreateComment("keep-alive end");
        _options.Insert(startAnchor, container, anchor);
        _options.Insert(endAnchor, container, anchor);
        TNode storage = _options.CreateElement(StorageContainerName);
        VirtualNode childValue = EvaluateSlot(value.Invocation, "default", owner)
            ?? new CommentNode(string.Empty);
        MountedNode<TNode> active = Mount(tree, childValue, container, endAnchor, owner);
        MountedKeepAlive<TNode> mounted = new(
            value,
            startAnchor,
            endAnchor,
            storage,
            active,
            owner);
        Register(tree, value, mounted);
        CacheActiveKeepAlive(tree, mounted, value, active);
        return mounted;
    }

    private void PatchKeepAlive(
        MountedTree<TNode> tree,
        MountedKeepAlive<TNode> mounted,
        KeepAliveNode next,
        TNode container)
    {
        PruneKeepAliveEntries(tree, mounted, next);
        VirtualNode nextValue = EvaluateSlot(next.Invocation, "default", mounted.Owner)
            ?? new CommentNode(string.Empty);
        MountedNode<TNode> current = mounted.Active;
        if (IsSameNodeType(current.Value, nextValue))
        {
            mounted.Active = Patch(
                tree,
                current,
                nextValue,
                container,
                mounted.EndAnchor,
                mounted.Owner);
            RefreshActiveKeepAlive(tree, mounted, next, mounted.Active);
            ReplaceValue(tree, mounted, next);
            return;
        }

        if (mounted.ActiveKey is { } outgoingKey
            && mounted.Cache.TryGetValue(outgoingKey, out KeepAliveCacheEntry<TNode>? outgoing)
            && ReferenceEquals(outgoing.Node, current))
        {
            Move(current, mounted.StorageContainer, default);
            QueueKeepAliveLifecycle(current, activate: false);
        }
        else
        {
            Unmount(tree, current, removeHostNodes: true);
        }

        mounted.ActiveKey = null;
        bool hasIncomingIdentity = TryGetKeepAliveIdentity(
            nextValue,
            out object? incomingKey,
            out string? incomingName);
        if (hasIncomingIdentity
            && mounted.Cache.TryGetValue(incomingKey, out KeepAliveCacheEntry<TNode>? cached)
            && IsSameNodeType(cached.Node.Value, nextValue))
        {
            cached.Node = Patch(
                tree,
                cached.Node,
                nextValue,
                mounted.StorageContainer,
                default,
                mounted.Owner);
            Move(cached.Node, container, mounted.EndAnchor);
            mounted.Active = cached.Node;
            mounted.ActiveKey = incomingKey;
            cached.ComponentName = incomingName;
            mounted.Touch(incomingKey);
            QueueKeepAliveLifecycle(cached.Node, activate: true);
        }
        else
        {
            if (hasIncomingIdentity
                && mounted.Cache.TryGetValue(
                    incomingKey,
                    out KeepAliveCacheEntry<TNode>? incompatible))
            {
                mounted.Remove(incomingKey);
                UnmountCachedKeepAliveEntry(tree, incompatible.Node);
            }

            mounted.Active = Mount(
                tree,
                nextValue,
                container,
                mounted.EndAnchor,
                mounted.Owner);
            CacheActiveKeepAlive(tree, mounted, next, mounted.Active);
        }

        EnforceKeepAliveMaximum(tree, mounted, next);
        ReplaceValue(tree, mounted, next);
    }

    private void CacheActiveKeepAlive(
        MountedTree<TNode> tree,
        MountedKeepAlive<TNode> mounted,
        KeepAliveNode boundary,
        MountedNode<TNode> active)
    {
        if (!TryGetKeepAliveIdentity(active.Value, out object? key, out string? name)
            || !ShouldKeepAlive(boundary.Invocation, name))
        {
            mounted.ActiveKey = null;
            return;
        }

        mounted.ActiveKey = key;
        mounted.Add(key, active, name);
        EnforceKeepAliveMaximum(tree, mounted, boundary);
        QueueKeepAliveLifecycle(active, activate: true);
    }

    private void RefreshActiveKeepAlive(
        MountedTree<TNode> tree,
        MountedKeepAlive<TNode> mounted,
        KeepAliveNode boundary,
        MountedNode<TNode> active)
    {
        if (!TryGetKeepAliveIdentity(active.Value, out object? key, out string? name)
            || !ShouldKeepAlive(boundary.Invocation, name))
        {
            if (mounted.ActiveKey is { } previousKey)
            {
                mounted.Remove(previousKey);
            }

            mounted.ActiveKey = null;
            return;
        }

        if (mounted.ActiveKey is { } activeKey
            && Equals(activeKey, key)
            && mounted.Cache.TryGetValue(activeKey, out KeepAliveCacheEntry<TNode>? entry))
        {
            entry.Node = active;
            entry.ComponentName = name;
            mounted.Touch(activeKey);
        }
        else
        {
            if (mounted.ActiveKey is { } previousKey)
            {
                mounted.Remove(previousKey);
            }

            mounted.Add(key, active, name);
        }

        mounted.ActiveKey = key;
        EnforceKeepAliveMaximum(tree, mounted, boundary);
    }

    private void PruneKeepAliveEntries(
        MountedTree<TNode> tree,
        MountedKeepAlive<TNode> mounted,
        KeepAliveNode boundary)
    {
        List<object> keys = [.. mounted.Cache.Keys];
        for (int index = 0; index < keys.Count; index++)
        {
            object key = keys[index];
            KeepAliveCacheEntry<TNode> entry = mounted.Cache[key];
            if (ShouldKeepAlive(boundary.Invocation, entry.ComponentName))
            {
                continue;
            }

            mounted.Remove(key);
            if (!ReferenceEquals(entry.Node, mounted.Active))
            {
                UnmountCachedKeepAliveEntry(tree, entry.Node);
            }
            else
            {
                mounted.ActiveKey = null;
            }
        }
    }

    private void EnforceKeepAliveMaximum(
        MountedTree<TNode> tree,
        MountedKeepAlive<TNode> mounted,
        KeepAliveNode boundary)
    {
        int maximum = ReadMaximum(boundary.Invocation);
        while (maximum > 0 && mounted.Cache.Count > maximum)
        {
            object key = mounted.Recency.First!.Value;
            KeepAliveCacheEntry<TNode> entry = mounted.Cache[key];
            mounted.Remove(key);
            if (ReferenceEquals(entry.Node, mounted.Active))
            {
                mounted.ActiveKey = null;
            }
            else
            {
                UnmountCachedKeepAliveEntry(tree, entry.Node);
            }
        }
    }

    private void UnmountCachedKeepAliveEntry(
        MountedTree<TNode> tree,
        MountedNode<TNode> mounted)
    {
        InvokeKeepAliveLifecycle(mounted, activate: false);
        Unmount(tree, mounted, removeHostNodes: true);
    }

    private static bool TryGetKeepAliveIdentity(
        VirtualNode value,
        out object key,
        out string? componentName)
    {
        if (value is not ComponentNode component)
        {
            key = null!;
            componentName = null;
            return false;
        }

        key = component.Key ?? component.Component;
        componentName = component.Component.RegisteredName
            ?? component.Component.ComponentType?.FullName;
        return true;
    }

    private static bool ShouldKeepAlive(
        ComponentInvocation invocation,
        string? componentName)
    {
        if (componentName is null)
        {
            return false;
        }

        invocation.Arguments.TryGetValue("include", out object? include);
        invocation.Arguments.TryGetValue("exclude", out object? exclude);
        return (include is null || MatchesComponentFilter(include, componentName))
            && (exclude is null || !MatchesComponentFilter(exclude, componentName));
    }

    private static bool MatchesComponentFilter(object filter, string componentName)
    {
        if (filter is Func<string, bool> predicate)
        {
            return predicate(componentName);
        }

        if (filter is string text)
        {
            string[] configuredNames = text.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int index = 0; index < configuredNames.Length; index++)
            {
                if (string.Equals(configuredNames[index], componentName, StringComparison.Ordinal)
                    || componentName.EndsWith(
                        string.Concat(".", configuredNames[index]),
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        if (filter is IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                if (string.Equals(name, componentName, StringComparison.Ordinal)
                    || componentName.EndsWith(
                        string.Concat(".", name),
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int ReadMaximum(ComponentInvocation invocation)
    {
        if (!invocation.Arguments.TryGetValue("maximum", out object? value)
            && !invocation.Arguments.TryGetValue("max", out value))
        {
            return 0;
        }

        return value switch
        {
            int number => number,
            long number when number <= int.MaxValue && number >= int.MinValue => (int)number,
            string text when int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int number) => number,
            _ => 0,
        };
    }

    private static void QueueKeepAliveLifecycle(
        MountedNode<TNode> mounted,
        bool activate)
    {
        QueuePostRenderEffect(mounted.EffectBoundary,
            new SchedulerJob(
                () => InvokeKeepAliveLifecycle(mounted, activate))
            {
                Name = activate
                    ? "keep-alive activated lifecycle"
                    : "keep-alive deactivated lifecycle",
            });
    }

    private static void InvokeKeepAliveLifecycle(
        MountedNode<TNode> mounted,
        bool activate)
    {
        switch (mounted)
        {
            case MountedComponent<TNode> component:
                if (component.IsUnmounted || component.Activation.IsReleased)
                {
                    return;
                }

                InvokeKeepAliveLifecycle(component.Subtree, activate);
                if (!activate && !component.HasKeepAliveLifecycleState)
                {
                    return;
                }

                if (component.HasKeepAliveLifecycleState
                    && component.IsKeepAliveLifecycleActive == activate)
                {
                    return;
                }

                component.HasKeepAliveLifecycleState = true;
                component.IsKeepAliveLifecycleActive = activate;
                component.Context.Run(
                    activate
                        ? component.Lifecycle.InvokeActivated
                        : component.Lifecycle.InvokeDeactivated);
                break;
            case MountedElement<TNode> element:
                InvokeKeepAliveLifecycle(element.Children, activate);
                break;
            case MountedRange<TNode> range:
                InvokeKeepAliveLifecycle(range.Children, activate);
                break;
            case MountedTeleport<TNode> teleport:
                InvokeKeepAliveLifecycle(teleport.Children, activate);
                break;
            case MountedKeepAlive<TNode> keepAlive:
                InvokeKeepAliveLifecycle(keepAlive.Active, activate);
                break;
            case MountedSuspense<TNode> suspense:
                InvokeKeepAliveLifecycle(suspense.ActiveBranch, activate);
                break;
            case MountedTransition<TNode> transition:
                InvokeKeepAliveLifecycle(CurrentTransitionChild(transition), activate);
                break;
        }
    }

    private static void InvokeKeepAliveLifecycle(
        IReadOnlyList<MountedNode<TNode>> children,
        bool activate)
    {
        for (int index = 0; index < children.Count; index++)
        {
            InvokeKeepAliveLifecycle(children[index], activate);
        }
    }

    private void UnmountKeepAlive(
        MountedTree<TNode> tree,
        MountedKeepAlive<TNode> mounted,
        bool removeHostNodes)
    {
        if (mounted.ActiveKey is { } activeKey
            && mounted.Cache.TryGetValue(activeKey, out KeepAliveCacheEntry<TNode>? activeEntry)
            && ReferenceEquals(activeEntry.Node, mounted.Active))
        {
            InvokeKeepAliveLifecycle(mounted.Active, activate: false);
        }

        HashSet<MountedNode<TNode>> released = new(ReferenceEqualityComparer.Instance);
        foreach (KeepAliveCacheEntry<TNode> entry in mounted.Cache.Values)
        {
            if (released.Add(entry.Node))
            {
                Unmount(
                    tree,
                    entry.Node,
                    removeHostNodes: !ReferenceEquals(entry.Node, mounted.Active));
            }
        }

        if (released.Add(mounted.Active))
        {
            Unmount(tree, mounted.Active, removeHostNodes: false);
        }

        if (removeHostNodes)
        {
            RemoveRange(mounted.StartAnchor, mounted.EndAnchor);
        }

        _options.Remove(mounted.StorageContainer);
        mounted.Cache.Clear();
        mounted.Recency.Clear();
        mounted.RecencyNodes.Clear();
    }

    private MountedSuspense<TNode> MountSuspense(
        MountedTree<TNode> tree,
        SuspenseNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        _ = ReadSuspenseTimeout(value);
        TNode startAnchor = _options.CreateComment("suspense start");
        TNode endAnchor = _options.CreateComment("suspense end");
        _options.Insert(startAnchor, container, anchor);
        _options.Insert(endAnchor, container, anchor);
        TNode storage = _options.CreateElement(StorageContainerName);
        SuspenseBoundary? previousBoundary = _activeSuspenseBoundary;
        SuspenseBoundary boundary = new(previousBoundary ?? owner?.SuspenseBoundary);
        MountedNode<TNode> content;
        _activeSuspenseBoundary = boundary;
        try
        {
            VirtualNode contentValue = EvaluateSlot(value.Invocation, "default", owner)
                ?? new CommentNode(string.Empty);
            content = Mount(tree, contentValue, storage, default, owner);
        }
        finally
        {
            _activeSuspenseBoundary = previousBoundary;
        }

        MountedNode<TNode> placeholder = Mount(tree, new CommentNode(string.Empty), container, endAnchor, owner);
        MountedSuspense<TNode> mounted = new(
            value, startAnchor, endAnchor, storage, boundary, content, null, placeholder, owner);
        FinishSuspenseMount(tree, mounted, container);
        return mounted;
    }

    private void FinishSuspenseMount(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        TNode container,
        bool isHydrating = false)
    {
        mounted.Container = container;
        Register(tree, mounted.Value, mounted);
        InitializeSuspense(tree, mounted);
        if (mounted.Boundary.PendingCount > 0)
        {
            int? timeout = ReadSuspenseTimeout((SuspenseNode)mounted.Value);
            BeginSuspensePending(tree, mounted, showFallback: isHydrating || timeout is null or 0);
        }
        else
        {
            ResolveSuspense(tree, mounted, container);
        }
    }

    private void InitializeSuspense(MountedTree<TNode> tree, MountedSuspense<TNode> mounted)
    {
        SuspenseBoundary boundary = mounted.Boundary;
        boundary.PendingStarted += () => Scheduler.QueueJob(new SchedulerJob(() =>
        {
            if (!mounted.IsUnmounted && ReferenceEquals(mounted.Boundary, boundary))
            {
                BeginSuspensePending(tree, mounted, showFallback: false);
            }
        }) { Name = "suspense pending" });
        boundary.Resolved += () => QueueSuspenseResolve(tree, mounted, mounted.Container);
        boundary.Failed += () =>
        {
            if (ReferenceEquals(mounted.Boundary, boundary))
            {
                mounted.TimeoutTimer?.Dispose();
                mounted.TimeoutTimer = null;
            }
        };
    }

    private void PatchSuspense(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        SuspenseNode next,
        TNode container)
    {
        _ = ReadSuspenseTimeout(next);
        mounted.Container = container;
        ReplaceValue(tree, mounted, next);
        VirtualNode contentValue = EvaluateSlot(next.Invocation, "default", mounted.Owner)
            ?? new CommentNode(string.Empty);
        bool replace = mounted.Boundary.IsFailed
            || !IsSameNodeType(mounted.ContentBranch.Value, contentValue);
        if (replace)
        {
            mounted.TimeoutTimer?.Dispose();
            mounted.TimeoutTimer = null;
            if (mounted.ResolveJob is not null)
            {
                mounted.ResolveJob.IsDisposed = true;
                mounted.ResolveJob = null;
            }

            if (!ReferenceEquals(mounted.ContentBranch, mounted.ActiveBranch))
            {
                mounted.Boundary.Dispose();
                Unmount(tree, mounted.ContentBranch, removeHostNodes: true);
            }
            else
            {
                mounted.Boundary.Retire();
            }

            mounted.Boundary = new SuspenseBoundary(mounted.Boundary.Parent);
            mounted.PendingEmitted = false;
            mounted.IsRevealing = false;
            InitializeSuspense(tree, mounted);
        }

        SuspenseBoundary? previousBoundary = _activeSuspenseBoundary;
        _activeSuspenseBoundary = mounted.Boundary;
        try
        {
            if (replace)
            {
                mounted.ContentBranch = Mount(tree, contentValue, mounted.StorageContainer, default, mounted.Owner);
            }
            else
            {
                bool visible = ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch);
                mounted.ContentBranch = Patch(
                    tree, mounted.ContentBranch, contentValue,
                    visible ? container : mounted.StorageContainer,
                    visible ? mounted.EndAnchor : default, mounted.Owner);
                if (visible)
                {
                    mounted.ActiveBranch = mounted.ContentBranch;
                }
            }
        }
        finally
        {
            _activeSuspenseBoundary = previousBoundary;
        }

        if (mounted.FallbackBranch is not null)
        {
            VirtualNode fallback = EvaluateSlot(next.Invocation, "fallback", mounted.Owner)
                ?? new CommentNode(string.Empty);
            mounted.FallbackBranch = Patch(tree, mounted.FallbackBranch, fallback, container, mounted.EndAnchor, mounted.Owner);
            mounted.ActiveBranch = mounted.FallbackBranch;
        }

        if (mounted.Boundary.PendingCount > 0)
        {
            BeginSuspensePending(tree, mounted, showFallback: false);
        }
        else
        {
            ResolveSuspense(tree, mounted, container);
        }
    }

    private void BeginSuspensePending(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        bool showFallback)
    {
        SuspenseBoundary boundary = mounted.Boundary;
        if (!boundary.IsPending || boundary.IsFailed || boundary.IsDisposed || mounted.PendingEmitted)
        {
            return;
        }

        mounted.PendingEmitted = true;
        EmitSuspenseEvent(tree, mounted, "pending");
        int? timeout = ReadSuspenseTimeout((SuspenseNode)mounted.Value);
        if (showFallback || timeout == 0)
        {
            ShowSuspenseFallback(tree, mounted);
        }
        else if (timeout is > 0 && mounted.FallbackBranch is null)
        {
            mounted.TimeoutTimer = Scheduler.ScheduleDelay(timeout.Value, () =>
            {
                if (!mounted.IsUnmounted && ReferenceEquals(boundary, mounted.Boundary)
                    && boundary.IsPending && !boundary.IsFailed && boundary.PendingCount > 0)
                {
                    ShowSuspenseFallback(tree, mounted);
                    QueueHostCommit();
                }
            });
        }
    }

    private void ShowSuspenseFallback(MountedTree<TNode> tree, MountedSuspense<TNode> mounted)
    {
        if (mounted.IsUnmounted || mounted.Boundary.IsFailed || mounted.FallbackBranch is not null)
        {
            return;
        }

        mounted.Container = HostParentOrFallback(mounted.EndAnchor, mounted.Container);
        if (ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch))
        {
            Move(mounted.ContentBranch, mounted.StorageContainer, default);
        }
        else
        {
            Unmount(tree, mounted.ActiveBranch, removeHostNodes: true);
            mounted.ActiveBoundary?.Dispose();
        }

        mounted.ActiveBoundary = null;
        VirtualNode fallback = EvaluateSlot(((SuspenseNode)mounted.Value).Invocation, "fallback", mounted.Owner)
            ?? new CommentNode(string.Empty);
        SuspenseBoundary? previousBoundary = _activeSuspenseBoundary;
        _activeSuspenseBoundary = mounted.Boundary.Parent;
        try
        {
            mounted.FallbackBranch = Mount(tree, fallback, mounted.Container, mounted.EndAnchor, mounted.Owner);
        }
        finally
        {
            _activeSuspenseBoundary = previousBoundary;
        }

        mounted.ActiveBranch = mounted.FallbackBranch;
        // Events observe committed presentation even when a deadline changes it during a flush.
        QueueHostCommit();
        _options.Commit?.Invoke();
        EmitSuspenseEvent(tree, mounted, "fallback");
    }

    private void QueueSuspenseResolve(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        TNode container)
    {
        if (mounted.ResolveJob is not null)
        {
            return;
        }

        SchedulerJob job = new(() =>
        {
            mounted.ResolveJob = null;
            if (!mounted.IsUnmounted)
            {
                ResolveSuspense(tree, mounted, mounted.Container);
            }
        }) { Name = "suspense reveal" };
        mounted.ResolveJob = job;
        Scheduler.QueuePostFlushCallback(job);
    }

    private void ResolveSuspense(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        TNode container)
    {
        SuspenseBoundary boundary = mounted.Boundary;
        if (boundary.PendingCount > 0 || !boundary.IsPending || boundary.IsFailed
            || boundary.IsDisposed || mounted.IsRevealing)
        {
            return;
        }

        mounted.TimeoutTimer?.Dispose();
        mounted.TimeoutTimer = null;
        mounted.IsRevealing = true;
        void Reveal()
        {
            if (mounted.IsUnmounted || !ReferenceEquals(boundary, mounted.Boundary)
                || boundary.IsFailed || boundary.IsDisposed)
            {
                return;
            }

            if (boundary.PendingCount > 0)
            {
                mounted.IsRevealing = false;
                return;
            }

            mounted.Container = HostParentOrFallback(mounted.EndAnchor, mounted.Container);
            if (!ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch))
            {
                Unmount(tree, mounted.ActiveBranch, removeHostNodes: true);
                mounted.ActiveBoundary?.Dispose();
                Move(mounted.ContentBranch, mounted.Container, mounted.EndAnchor);
            }

            mounted.ActiveBranch = mounted.ContentBranch;
            mounted.ActiveBoundary = boundary;
            mounted.FallbackBranch = null;
            mounted.IsRevealing = false;
            mounted.PendingEmitted = false;
            QueueHostCommit();
            // Reveal can finish inside the post-flush phase or an asynchronous leave callback.
            // Commit its moves before releasing references and mounted callbacks [SCH-10].
            _options.Commit?.Invoke();
            EmitSuspenseEvent(tree, mounted, "resolve");
            boundary.Reveal();
        }

        if (!ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch)
            && mounted.ActiveBranch is MountedTransition<TNode> transition
            && !transition.Controller.Properties.Persisted)
        {
            SettleTransitionBeforePatch(tree, transition);
            BeginTransitionLeave(tree, transition, CurrentTransitionChild(transition), transition.Controller, _ => Reveal());
        }
        else
        {
            Reveal();
        }
    }

    private static int? ReadSuspenseTimeout(SuspenseNode value)
    {
        if (!value.Invocation.Arguments.TryGetValue("timeout", out object? argument) || argument is null)
        {
            return null;
        }

        if (argument is not int timeout)
        {
            throw new ArgumentException("The Suspense timeout argument must be an integer number of milliseconds.", nameof(value));
        }

        return timeout < 0 ? null : timeout;
    }

    private static void EmitSuspenseEvent(MountedTree<TNode> tree, MountedSuspense<TNode> mounted, string name)
    {
        if (!((SuspenseNode)mounted.Value).Invocation.Listeners.TryGetValue(name, out ComponentEventListener? listener))
        {
            return;
        }

        try
        {
            if (mounted.Owner is { } owner)
            {
                owner.Run(() => listener(Array.Empty<object?>()));
            }
            else
            {
                listener(Array.Empty<object?>());
            }
        }
        catch (Exception error)
        {
            if (mounted.Owner is { } owner)
            {
                owner.RouteError(error, $"suspense {name} event listener");
            }
            else if (tree.Application?.ErrorHandler is { } handler)
            {
                handler(error, null, $"suspense {name} event listener");
            }
            else
            {
                throw;
            }
        }
    }

    private void UnmountSuspense(MountedTree<TNode> tree, MountedSuspense<TNode> mounted, bool removeHostNodes)
    {
        mounted.TimeoutTimer?.Dispose();
        mounted.TimeoutTimer = null;
        if (mounted.ResolveJob is not null)
        {
            mounted.ResolveJob.IsDisposed = true;
            Scheduler.InvalidateJob(mounted.ResolveJob);
            mounted.ResolveJob = null;
        }

        mounted.Boundary.Dispose();
        mounted.ActiveBoundary?.Dispose();
        if (!ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch))
        {
            Unmount(tree, mounted.ActiveBranch, removeHostNodes: false);
        }

        Unmount(tree, mounted.ContentBranch, removeHostNodes: !ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch));
        if (removeHostNodes)
        {
            RemoveRange(mounted.StartAnchor, mounted.EndAnchor);
        }

        _options.Remove(mounted.StorageContainer);
    }

    private MountedTransition<TNode> MountTransition(
        MountedTree<TNode> tree,
        TransitionNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        VirtualNode childValue = EvaluateSlot(value.Invocation, "default", owner)
            ?? new CommentNode(string.Empty);
        TransitionProperties properties = ResolveTransitionProperties(value);
        TransitionState sharedState = ResolveTransitionState(value) ?? new TransitionState();
        TransitionController controller = CreateTransitionController(
            tree,
            owner,
            properties,
            sharedState,
            childValue);
        bool shouldEnter = !properties.Persisted
            && (sharedState.IsMounted || properties.Appear);
        SuspenseBoundary? effectBoundary = _activeSuspenseBoundary ?? owner?.SuspenseBoundary;
        bool deferEnter = shouldEnter && effectBoundary is { IsHidden: true };
        (MountedNode<TNode> child, TransitionMountContext<TNode> mountContext) =
            MountTransitionChild(
                tree,
                childValue,
                container,
                anchor,
                owner,
                controller,
                shouldEnter && !deferEnter);
        MountedTransition<TNode> mounted = new(
            value,
            child,
            sharedState,
            controller,
            owner)
        {
            State = TransitionExecutionState.Entered,
        };
        Register(tree, value, mounted);
        if (deferEnter)
        {
            effectBoundary!.QueueEffect(new SchedulerJob(() =>
            {
                if (!mounted.IsUnmounted)
                {
                    BeginTransitionEnter(tree, mounted, mounted.Child, controller, mountContext);
                }
            }) { Name = "suspense transition enter" });
        }
        else if (shouldEnter)
        {
            BeginTransitionEnter(
                tree,
                mounted,
                child,
                controller,
                mountContext);
        }

        QueueTransitionMountedState(mounted, controller, sharedState);

        return mounted;
    }

    private void PatchTransition(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        TransitionNode next,
        TNode container)
    {
        SettleTransitionBeforePatch(tree, mounted);
        IReadOnlyList<TransitionElementSnapshot> outgoingSnapshot =
            CaptureTransitionSnapshot(CurrentTransitionChild(mounted));
        ObserveTransitionBeforeUpdate(mounted.Controller, outgoingSnapshot);
        VirtualNode nextValue = EvaluateSlot(next.Invocation, "default", mounted.Owner)
            ?? new CommentNode(string.Empty);
        TransitionProperties nextProperties = ResolveTransitionProperties(next);
        TransitionState nextState = ResolveTransitionState(next) ?? mounted.SharedState;
        MountedNode<TNode> current = CurrentTransitionChild(mounted);
        TransitionController currentController = mounted.Controller;
        bool wasPersisted = currentController.Properties.Persisted;
        bool isSameNodeType = IsSameNodeType(current.Value, nextValue);
        bool preservePersistedController = wasPersisted
            && nextProperties.Persisted
            && isSameNodeType
            && ReferenceEquals(nextState, mounted.SharedState);
        TransitionController nextController;
        if (preservePersistedController)
        {
            currentController.UpdateProperties(nextProperties);
            nextController = currentController;
        }
        else
        {
            nextController = CreateTransitionController(
                tree,
                mounted.Owner,
                nextProperties,
                nextState,
                nextValue);
        }

        if (wasPersisted
            || nextProperties.Persisted
            || isSameNodeType)
        {
            MountedNode<TNode> patched = PatchTransitionChild(
                tree,
                current,
                nextValue,
                container,
                GetNextHostNode(current),
                mounted.Owner,
                nextController);
            if (!ReferenceEquals(currentController, nextController))
            {
                currentController.Dispose();
            }

            mounted.Child = patched;
            mounted.Controller = nextController;
            mounted.SharedState = nextState;
            mounted.IncomingChild = null;
            mounted.Overlap = null;
            mounted.State = TransitionExecutionState.Entered;
            ReplaceValue(tree, mounted, next);
            ObserveTransitionUpdated(
                nextController,
                CaptureTransitionSnapshot(mounted.Child));
            return;
        }

        switch (ReadTransitionMode(nextProperties))
        {
            case TransitionMode.OutgoingThenIncoming:
                PatchTransitionOutgoingThenIncoming(
                    tree,
                    mounted,
                    next,
                    nextValue,
                    nextController,
                    nextState,
                    container);
                break;
            case TransitionMode.IncomingThenOutgoing:
                PatchTransitionWithOverlap(
                    tree,
                    mounted,
                    next,
                    nextValue,
                    nextController,
                    nextState,
                    container,
                    delayLeaveUntilEnter: true);
                break;
            default:
                PatchTransitionWithOverlap(
                    tree,
                    mounted,
                    next,
                    nextValue,
                    nextController,
                    nextState,
                    container,
                    delayLeaveUntilEnter: false);
                break;
        }
    }

    private void PatchTransitionOutgoingThenIncoming(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        TransitionNode next,
        VirtualNode nextValue,
        TransitionController nextController,
        TransitionState nextState,
        TNode container)
    {
        MountedNode<TNode> outgoing = mounted.Child;
        TransitionController outgoingController = mounted.Controller;
        TNode? insertionAnchor = GetNextHostNode(outgoing);
        ReplaceValue(tree, mounted, next);
        bool outgoingRemoved = false;
        BeginTransitionLeave(
            tree,
            mounted,
            outgoing,
            outgoingController,
            _ =>
            {
                if (mounted.IsUnmounted || !ReferenceEquals(mounted.Child, outgoing))
                {
                    return;
                }

                Unmount(tree, outgoing, removeHostNodes: true);
                outgoingController.Dispose();
                outgoingRemoved = true;
            },
            cancelled =>
            {
                if (!outgoingRemoved || mounted.IsUnmounted)
                {
                    return;
                }

                (MountedNode<TNode> incoming, TransitionMountContext<TNode> mountContext) =
                    MountTransitionChild(
                        tree,
                        nextValue,
                        container,
                        insertionAnchor,
                        mounted.Owner,
                        nextController,
                        shouldEnter: !cancelled && !nextController.Properties.Persisted);
                mounted.Child = incoming;
                mounted.Controller = nextController;
                mounted.SharedState = nextState;
                mounted.State = TransitionExecutionState.Entered;
                if (!cancelled)
                {
                    BeginTransitionEnter(
                        tree,
                        mounted,
                        incoming,
                        nextController,
                        mountContext);
                }

                NormalizeTeleportTargetOrder(tree);
                ObserveTransitionUpdated(
                    nextController,
                    CaptureTransitionSnapshot(incoming));
                QueueTransitionMountedState(mounted, nextController, nextState);
            });
    }

    private void PatchTransitionWithOverlap(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        TransitionNode next,
        VirtualNode nextValue,
        TransitionController nextController,
        TransitionState nextState,
        TNode container,
        bool delayLeaveUntilEnter)
    {
        MountedNode<TNode> outgoing = mounted.Child;
        TransitionController outgoingController = mounted.Controller;
        TNode? insertionAnchor = GetNextHostNode(outgoing);
        TNode startAnchor = _options.CreateComment("transition overlap start");
        TNode endAnchor = _options.CreateComment("transition overlap end");
        _options.Insert(startAnchor, container, outgoing.FirstHostNode);
        _options.Insert(endAnchor, container, insertionAnchor);
        (MountedNode<TNode> incoming, TransitionMountContext<TNode> mountContext) =
            MountTransitionChild(
                tree,
                nextValue,
                container,
                endAnchor,
                mounted.Owner,
                nextController,
                shouldEnter: !nextController.Properties.Persisted);
        FragmentNode overlapValue = new([outgoing.Value, incoming.Value]);
        MountedRange<TNode> overlap = new(
            overlapValue,
            startAnchor,
            endAnchor,
            [outgoing, incoming],
            mounted.Owner);
        Register(tree, overlapValue, overlap);
        mounted.Child = overlap;
        mounted.Overlap = overlap;
        mounted.IncomingChild = incoming;
        mounted.Controller = nextController;
        mounted.SharedState = nextState;
        ReplaceValue(tree, mounted, next);

        void BeginLeaveAfterEnter(bool _)
        {
            if (mounted.IsUnmounted || !ReferenceEquals(mounted.Overlap, overlap))
            {
                return;
            }

            BeginTransitionLeave(
                tree,
                mounted,
                outgoing,
                outgoingController,
                cancelled => CompleteTransitionOverlap(
                    tree,
                    mounted,
                    overlap,
                    outgoing,
                    incoming,
                    cancelled,
                    outgoingController),
                afterCompletion: null);
        }

        BeginTransitionEnter(
            tree,
            mounted,
            incoming,
            nextController,
            mountContext,
            delayLeaveUntilEnter ? BeginLeaveAfterEnter : null);
        ObserveTransitionUpdated(
            nextController,
            CaptureTransitionSnapshot(incoming));
        if (!delayLeaveUntilEnter)
        {
            BeginLeaveAfterEnter(false);
        }

        QueueTransitionMountedState(mounted, nextController, nextState);
    }

    private void CompleteTransitionOverlap(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        MountedRange<TNode> overlap,
        MountedNode<TNode> outgoing,
        MountedNode<TNode> incoming,
        bool cancelled,
        TransitionController outgoingController)
    {
        _ = cancelled;
        if (mounted.IsUnmounted || !ReferenceEquals(mounted.Overlap, overlap))
        {
            return;
        }

        Unmount(tree, outgoing, removeHostNodes: true);
        outgoingController.Dispose();
        _options.Remove(overlap.StartAnchor);
        _options.Remove(overlap.EndAnchor);
        Unregister(tree, overlap);
        overlap.IsUnmounted = true;
        overlap.Children.Clear();
        mounted.Child = incoming;
        mounted.IncomingChild = null;
        mounted.Overlap = null;
        mounted.State = mounted.EnterOperation is null
            ? TransitionExecutionState.Entered
            : TransitionExecutionState.Entering;
    }

    private void SettleTransitionBeforePatch(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted)
    {
        FinishPendingTransitionEnter(tree, mounted);
        CancelPendingTransitionLeave(tree, mounted);
        FinishPendingTransitionEnter(tree, mounted);
    }

    private void UnmountTransition(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        bool removeHostNodes)
    {
        tree.PendingTransitionRemovals.Remove(mounted);
        mounted.IsUnmounted = true;
        TransitionOperation<TNode>? enter = mounted.EnterOperation;
        if (enter is not null)
        {
            enter.Controller.FinishEnter(enter.Element!, cancelled: true);
        }

        TransitionOperation<TNode>? leave = mounted.LeaveOperation;
        if (leave is not null)
        {
            leave.Controller.FinishLeave(leave.Element!, cancelled: false);
        }

        mounted.Controller.Drain();
        Unmount(tree, mounted.Child, removeHostNodes);
        mounted.Controller.Dispose();
        mounted.IncomingChild = null;
        mounted.Overlap = null;
        mounted.State = TransitionExecutionState.Left;
    }

    private bool TryDeferTransitionUnmount(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted)
    {
        if (mounted.Controller.Properties.Persisted)
        {
            return false;
        }

        if (mounted.IsUnmountPending)
        {
            return true;
        }

        SettleTransitionBeforePatch(tree, mounted);
        if (mounted.IsUnmounted)
        {
            return true;
        }

        MountedNode<TNode> child = CurrentTransitionChild(mounted);
        mounted.IsUnmountPending = true;
        tree.PendingTransitionRemovals.Add(mounted);
        BeginTransitionLeave(
            tree,
            mounted,
            child,
            mounted.Controller,
            _ => FinalizeDeferredTransitionUnmount(
                tree,
                mounted));
        return true;
    }

    private void FinalizeDeferredTransitionUnmount(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted)
    {
        if (mounted.IsUnmounted)
        {
            return;
        }

        ClearReference(tree, mounted);
        UnmountTransition(tree, mounted, removeHostNodes: true);
        Unregister(tree, mounted);
    }

    private void FinishPendingTransitionEnter(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted)
    {
        TransitionOperation<TNode>? operation = mounted.EnterOperation;
        if (operation is null)
        {
            return;
        }

        _ = tree;
        operation.Controller.FinishEnter(operation.Element!, cancelled: false);
    }

    private void CancelPendingTransitionLeave(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted)
    {
        TransitionOperation<TNode>? operation = mounted.LeaveOperation;
        if (operation is null)
        {
            return;
        }

        _ = tree;
        operation.Controller.FinishLeave(operation.Element!, cancelled: true);
    }

    private void BeginTransitionEnter(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        MountedNode<TNode> child,
        TransitionController controller,
        TransitionMountContext<TNode> mountContext,
        Action<bool>? afterCompletion = null)
    {
        if (mounted.EffectBoundary is { IsHidden: true } boundary)
        {
            boundary.QueueEffect(new SchedulerJob(() =>
            {
                if (!mounted.IsUnmounted)
                {
                    BeginTransitionEnter(tree, mounted, child, controller, mountContext, afterCompletion);
                }
            }) { Name = "suspense transition enter" });
            return;
        }

        if (controller.Properties.Persisted || mountContext.IsSuppressed)
        {
            mounted.State = TransitionExecutionState.Entered;
            afterCompletion?.Invoke(false);
            return;
        }

        TNode element;
        if (HasHostNode(mountContext.Element))
        {
            element = mountContext.Element!;
        }
        else if (!TryGetFirstTransitionElement(child, out element))
        {
            mounted.State = TransitionExecutionState.Entered;
            afterCompletion?.Invoke(false);
            return;
        }

        if (!mountContext.BeforeEnterInvoked && !controller.BeforeEnter(element!))
        {
            mounted.State = TransitionExecutionState.Entered;
            afterCompletion?.Invoke(false);
            return;
        }

        TransitionOperation<TNode> operation = new(
            TransitionOperationKind.Enter,
            element,
            controller,
            afterCompletion ?? (static _ => { }));
        mounted.EnterOperation = operation;
        mounted.State = TransitionExecutionState.Entering;
        controller.Enter(
            element!,
            cancelled => CompleteTransitionOperation(mounted, operation, cancelled));
        _ = tree;
    }

    private void BeginTransitionLeave(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        MountedNode<TNode> child,
        TransitionController controller,
        Action<bool> removal,
        Action<bool>? afterCompletion = null)
    {
        if (controller.Properties.Persisted
            || !TryGetFirstTransitionElement(child, out TNode element))
        {
            removal(false);
            afterCompletion?.Invoke(false);
            return;
        }

        TransitionOperation<TNode> operation = new(
            TransitionOperationKind.Leave,
            element,
            controller,
            removal);
        mounted.LeaveOperation = operation;
        mounted.State = TransitionExecutionState.Leaving;
        controller.Leave(
            element!,
            cancelled => CompleteTransitionOperation(mounted, operation, cancelled),
            afterCompletion);
        _ = tree;
    }

    private void CancelPendingTransitionEnter(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted)
    {
        TransitionOperation<TNode>? operation = mounted.EnterOperation;
        if (operation is null)
        {
            return;
        }

        _ = tree;
        operation.Controller.FinishEnter(operation.Element!, cancelled: true);
    }

    private void CompleteTransitionOperation(
        MountedTransition<TNode> mounted,
        TransitionOperation<TNode> operation,
        bool cancelled)
    {
        if (operation.IsCompleted || mounted.IsUnmounted)
        {
            operation.IsCompleted = true;
            return;
        }

        bool isEnter = operation.Kind == TransitionOperationKind.Enter;
        TransitionOperation<TNode>? active = isEnter
            ? mounted.EnterOperation
            : mounted.LeaveOperation;
        if (!ReferenceEquals(active, operation))
        {
            operation.IsCompleted = true;
            return;
        }

        operation.IsCompleted = true;
        if (isEnter)
        {
            mounted.EnterOperation = null;
        }
        else
        {
            mounted.LeaveOperation = null;
        }

        operation.Completion(cancelled);
        if (!mounted.IsUnmounted)
        {
            mounted.State = mounted.LeaveOperation is not null
                ? TransitionExecutionState.Leaving
                : mounted.EnterOperation is not null
                    ? TransitionExecutionState.Entering
                    : TransitionExecutionState.Entered;
        }

        QueueTransitionHostCommit();
    }

    private void QueueTransitionHostCommit()
    {
        if (_options.Commit is null)
        {
            return;
        }

        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(QueueHostCommit)
            {
                Name = "transition host commit",
            });
    }

    private static void ObserveTransitionBeforeUpdate(
        TransitionController controller,
        IReadOnlyList<TransitionElementSnapshot> outgoing) =>
        controller.ObserveBeforeUpdate(outgoing);

    private static void ObserveTransitionUpdated(
        TransitionController controller,
        IReadOnlyList<TransitionElementSnapshot> incoming) =>
        controller.ObserveUpdated(incoming);

    private static IReadOnlyList<TransitionElementSnapshot> CaptureTransitionSnapshot(
        MountedNode<TNode> mounted)
    {
        List<TransitionElementSnapshot> snapshot = [];
        CollectTransitionSnapshotEntries(mounted, snapshot);
        return snapshot.AsReadOnly();
    }

    private static void CollectTransitionSnapshotEntries(
        MountedNode<TNode> mounted,
        List<TransitionElementSnapshot> snapshot)
    {
        if (mounted.Value.Key is { } key
            && TryGetFirstTransitionElement(mounted, out TNode keyedElement))
        {
            snapshot.Add(new TransitionElementSnapshot(key, keyedElement));
            return;
        }

        switch (mounted)
        {
            case MountedElement<TNode> element:
                CollectTransitionSnapshotEntries(element.Children, snapshot);
                break;
            case MountedRange<TNode> range:
                CollectTransitionSnapshotEntries(range.Children, snapshot);
                break;
            case MountedComponent<TNode> component:
                CollectTransitionSnapshotEntries(component.Subtree, snapshot);
                break;
            case MountedTeleport<TNode> teleport:
                CollectTransitionSnapshotEntries(teleport.Children, snapshot);
                break;
            case MountedKeepAlive<TNode> keepAlive:
                CollectTransitionSnapshotEntries(keepAlive.Active, snapshot);
                break;
            case MountedSuspense<TNode> suspense:
                CollectTransitionSnapshotEntries(suspense.ActiveBranch, snapshot);
                break;
            case MountedTransition<TNode> transition:
                CollectTransitionSnapshotEntries(
                    CurrentTransitionChild(transition),
                    snapshot);
                break;
        }
    }

    private static void CollectTransitionSnapshotEntries(
        IReadOnlyList<MountedNode<TNode>> mounted,
        List<TransitionElementSnapshot> snapshot)
    {
        for (int index = 0; index < mounted.Count; index++)
        {
            CollectTransitionSnapshotEntries(mounted[index], snapshot);
        }
    }

    private static bool TryGetFirstTransitionElement(
        MountedNode<TNode> mounted,
        out TNode element)
    {
        switch (mounted)
        {
            case MountedElement<TNode> mountedElement:
                element = mountedElement.HostNode;
                return true;
            case MountedComponent<TNode> component:
                return TryGetFirstTransitionElement(component.Subtree, out element);
            case MountedRange<TNode> range:
                for (int index = 0; index < range.Children.Count; index++)
                {
                    if (TryGetFirstTransitionElement(range.Children[index], out element))
                    {
                        return true;
                    }
                }

                break;
            case MountedTeleport<TNode> teleport:
                for (int index = 0; index < teleport.Children.Count; index++)
                {
                    if (TryGetFirstTransitionElement(teleport.Children[index], out element))
                    {
                        return true;
                    }
                }

                break;
            case MountedKeepAlive<TNode> keepAlive:
                return TryGetFirstTransitionElement(keepAlive.Active, out element);
            case MountedSuspense<TNode> suspense:
                return TryGetFirstTransitionElement(suspense.ActiveBranch, out element);
            case MountedTransition<TNode> transition:
                return TryGetFirstTransitionElement(
                    CurrentTransitionChild(transition),
                    out element);
        }

        element = default!;
        return false;
    }

    private bool TryInvokeTransitionHostCallback(
        MountedTree<TNode> tree,
        RuntimeComponentContext? owner,
        Action callback,
        string information)
    {
        try
        {
            if (owner is null)
            {
                callback();
            }
            else
            {
                owner.Run(callback);
            }

            return true;
        }
        catch (TransitionContinuationException)
        {
            // The controller unwraps this signal after leaving the authored-hook boundary.
            throw;
        }
        catch (Exception exception)
        {
            if (owner is not null)
            {
                owner.RouteError(exception, information);
            }
            else if (tree.Application?.ErrorHandler is { } errorHandler)
            {
                errorHandler(exception, null, information);
            }
            else
            {
                throw;
            }

            return false;
        }
    }

    private static MountedNode<TNode> CurrentTransitionChild(
        MountedTransition<TNode> mounted) =>
        mounted.IncomingChild ?? mounted.Child;

    private static TransitionMode ReadTransitionMode(TransitionProperties properties)
    {
        return properties.Mode switch
        {
            "out-in" => TransitionMode.OutgoingThenIncoming,
            "in-out" => TransitionMode.IncomingThenOutgoing,
            _ => TransitionMode.Simultaneous,
        };
    }

    private TransitionController CreateTransitionController(
        MountedTree<TNode> tree,
        RuntimeComponentContext? owner,
        TransitionProperties properties,
        TransitionState state,
        VirtualNode child) =>
        new(
            properties,
            state,
            TransitionIdentity.Create(child),
            (callback, information) => TryInvokeTransitionHostCallback(
                tree,
                owner,
                callback,
                information));

    private (MountedNode<TNode> Child, TransitionMountContext<TNode> Context)
        MountTransitionChild(
            MountedTree<TNode> tree,
            VirtualNode child,
            TNode container,
            TNode? anchor,
            RuntimeComponentContext? owner,
            TransitionController controller,
            bool shouldEnter)
    {
        TransitionMountContext<TNode>? previous = _activeTransitionMount;
        TransitionMountContext<TNode> context = new(
            controller,
            previous,
            shouldEnter && !((_activeSuspenseBoundary ?? owner?.SuspenseBoundary)?.IsHidden ?? false),
            isHydrating: false);
        _activeTransitionMount = context;
        try
        {
            return (Mount(tree, child, container, anchor, owner), context);
        }
        finally
        {
            _activeTransitionMount = previous;
        }
    }

    private MountedNode<TNode> PatchTransitionChild(
        MountedTree<TNode> tree,
        MountedNode<TNode> current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner,
        TransitionController controller)
    {
        TransitionMountContext<TNode>? previous = _activeTransitionMount;
        _activeTransitionMount = new TransitionMountContext<TNode>(
            controller,
            previous,
            shouldEnter: false,
            isHydrating: false);
        try
        {
            return Patch(
                tree,
                current,
                next,
                container,
                anchor,
                owner,
                allowTransitionLeave: false);
        }
        finally
        {
            _activeTransitionMount = previous;
        }
    }

    private static void QueueTransitionMountedState(
        MountedTransition<TNode> mounted,
        TransitionController controller,
        TransitionState state)
    {
        QueuePostRenderEffect(mounted.EffectBoundary,
            new SchedulerJob(
                () =>
                {
                    if (!mounted.IsUnmounted)
                    {
                        state.IsMounted = true;
                    }

                    controller.IsHydrating = false;
                })
            {
                Name = "transition mounted state",
            });
    }

    private static TransitionState? ResolveTransitionState(TransitionNode transition) =>
        transition.Invocation.Arguments.TryGetValue(
            TransitionProperties.StateArgument,
            out object? value)
            ? value as TransitionState
            : null;

    private static TransitionProperties ResolveTransitionProperties(
        TransitionNode transition)
    {
        IReadOnlyDictionary<string, object?> arguments = transition.Invocation.Arguments;
        if (arguments.TryGetValue(
                TransitionProperties.ResolvedArgument,
                out object? value)
            && value is TransitionProperties properties)
        {
            return properties;
        }

        return new TransitionProperties
        {
            Mode = arguments.TryGetValue("mode", out value) ? value as string : null,
            Appear = arguments.TryGetValue("appear", out value) && value is true,
            Persisted = arguments.TryGetValue("persisted", out value) && value is true,
            OnBeforeEnter = ReadTransitionAction(arguments, "onBeforeEnter"),
            OnEnter = ReadTransitionPhase(arguments, "onEnter"),
            OnAfterEnter = ReadTransitionAction(arguments, "onAfterEnter"),
            OnEnterCancelled = ReadTransitionAction(arguments, "onEnterCancelled"),
            OnBeforeLeave = ReadTransitionAction(arguments, "onBeforeLeave"),
            OnLeave = ReadTransitionPhase(arguments, "onLeave"),
            OnAfterLeave = ReadTransitionAction(arguments, "onAfterLeave"),
            OnLeaveCancelled = ReadTransitionAction(arguments, "onLeaveCancelled"),
            OnBeforeAppear = ReadTransitionAction(arguments, "onBeforeAppear"),
            OnAppear = ReadTransitionPhase(arguments, "onAppear"),
            OnAfterAppear = ReadTransitionAction(arguments, "onAfterAppear"),
            OnAppearCancelled = ReadTransitionAction(arguments, "onAppearCancelled"),
            OnBeforeUpdate = ReadTransitionSnapshotObserver(arguments, "onBeforeUpdate"),
            OnUpdated = ReadTransitionSnapshotObserver(arguments, "onUpdated"),
        };
    }

    private static Action<object>? ReadTransitionAction(
        IReadOnlyDictionary<string, object?> arguments,
        string name) =>
        arguments.TryGetValue(name, out object? value)
            ? value as Action<object>
            : null;

    private static TransitionPhaseHook? ReadTransitionPhase(
        IReadOnlyDictionary<string, object?> arguments,
        string name)
    {
        if (!arguments.TryGetValue(name, out object? value))
        {
            return null;
        }

        return value switch
        {
            TransitionPhaseHook hook => hook,
            Action<object> action => (element, complete) =>
            {
                action(element);
                complete();
            }
            ,
            _ => null,
        };
    }

    private static Action<IReadOnlyList<TransitionElementSnapshot>>?
        ReadTransitionSnapshotObserver(
            IReadOnlyDictionary<string, object?> arguments,
            string name) =>
        arguments.TryGetValue(name, out object? value)
            ? value as Action<IReadOnlyList<TransitionElementSnapshot>>
            : null;

    private static VirtualNode? EvaluateSlot(
        ComponentInvocation invocation,
        string name,
        RuntimeComponentContext? owner)
    {
        if (!invocation.Slots.TryGetValue(name, out ComponentSlot? slot))
        {
            return null;
        }

        return owner is null
            ? slot(EmptySlotArguments)
            : owner.Run(() => slot(EmptySlotArguments));
    }

    private void MoveChildren(
        IReadOnlyList<MountedNode<TNode>> children,
        TNode container,
        TNode? anchor)
    {
        for (int index = 0; index < children.Count; index++)
        {
            Move(children[index], container, anchor);
        }
    }
}
