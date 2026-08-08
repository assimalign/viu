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
        Register(tree, value, mounted);

        if (value.IsDisabled)
        {
            mounted.Children = MountChildren(
                tree,
                value.Children,
                container,
                endAnchor,
                owner);
            mounted.ChildrenMounted = true;
            TryInstallTeleportTargetAnchor(tree, mounted, value);
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
        ReplaceValue(tree, mounted, next);

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

            EnsureTeleportTarget(
                tree,
                mounted,
                next,
                moveChildrenToTarget: false);
            return;
        }

        if (next.IsDeferred)
        {
            if (mounted.ChildrenMounted)
            {
                UnmountChildren(tree, mounted.Children, removeHostNodes: true);
                mounted.Children.Clear();
                mounted.ChildrenMounted = false;
            }

            RemoveTeleportTargetAnchor(mounted);
            QueueDeferredTeleportMount(tree, mounted, originContainer);
            return;
        }

        if (!EnsureTeleportTarget(
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
            && TryPatchBlockChildren(tree, previous, next, container);
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
                tree,
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
        SchedulerJob job = new(
            () =>
            {
                if (mounted.IsUnmounted
                    || mounted.Value is not TeleportNode current
                    || current.IsDisabled
                    || !current.IsDeferred)
                {
                    return;
                }

                if (!EnsureTeleportTarget(tree, mounted, current))
                {
                    return;
                }

                mounted.Children = MountChildren(
                    tree,
                    current.Children,
                    mounted.TargetContainer!,
                    mounted.TargetAnchor,
                    mounted.Owner);
                mounted.ChildrenMounted = true;
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
        if (TryGetKeepAliveIdentity(nextValue, out object? incomingKey, out string? incomingName)
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
                Unmount(tree, entry.Node, removeHostNodes: true);
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
                Unmount(tree, entry.Node, removeHostNodes: true);
            }
        }
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
        Scheduler.QueuePostFlushCallback(
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
        TNode startAnchor = _options.CreateComment("suspense start");
        TNode endAnchor = _options.CreateComment("suspense end");
        _options.Insert(startAnchor, container, anchor);
        _options.Insert(endAnchor, container, anchor);
        TNode storage = _options.CreateElement(StorageContainerName);
        SuspenseBoundary boundary = new();
        SuspenseBoundary? previousBoundary = _activeSuspenseBoundary;
        _activeSuspenseBoundary = boundary;
        MountedNode<TNode> content;
        try
        {
            VirtualNode contentValue = EvaluateSlot(value.Invocation, "default", owner)
                ?? new CommentNode(string.Empty);
            content = Mount(tree, contentValue, container, endAnchor, owner);
        }
        finally
        {
            _activeSuspenseBoundary = previousBoundary;
        }

        MountedNode<TNode>? fallback = null;
        MountedNode<TNode> active = content;
        if (boundary.PendingCount > 0)
        {
            Move(content, storage, default);
            VirtualNode fallbackValue = EvaluateSlot(value.Invocation, "fallback", owner)
                ?? new CommentNode(string.Empty);
            fallback = Mount(tree, fallbackValue, container, endAnchor, owner);
            active = fallback;
        }

        MountedSuspense<TNode> mounted = new(
            value,
            startAnchor,
            endAnchor,
            storage,
            boundary,
            content,
            fallback,
            active,
            owner);
        boundary.Resolved += () => QueueSuspenseResolve(tree, mounted, container);
        Register(tree, value, mounted);
        return mounted;
    }

    private void PatchSuspense(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        SuspenseNode next,
        TNode container)
    {
        SuspenseNode previous = (SuspenseNode)mounted.Value;
        SuspenseBoundary? previousBoundary = _activeSuspenseBoundary;
        _activeSuspenseBoundary = mounted.Boundary;
        try
        {
            VirtualNode contentValue = EvaluateSlot(next.Invocation, "default", mounted.Owner)
                ?? new CommentNode(string.Empty);
            TNode contentContainer = ReferenceEquals(
                mounted.ActiveBranch,
                mounted.ContentBranch)
                    ? container
                    : mounted.StorageContainer;
            TNode? contentAnchor = ReferenceEquals(
                mounted.ActiveBranch,
                mounted.ContentBranch)
                    ? mounted.EndAnchor
                    : default;
            mounted.ContentBranch = Patch(
                tree,
                mounted.ContentBranch,
                contentValue,
                contentContainer,
                contentAnchor,
                mounted.Owner);
        }
        finally
        {
            _activeSuspenseBoundary = previousBoundary;
        }

        if (mounted.Boundary.PendingCount > 0)
        {
            if (ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch))
            {
                Move(mounted.ContentBranch, mounted.StorageContainer, default);
                VirtualNode fallbackValue = EvaluateSlot(
                    next.Invocation,
                    "fallback",
                    mounted.Owner) ?? new CommentNode(string.Empty);
                mounted.FallbackBranch = Mount(
                    tree,
                    fallbackValue,
                    container,
                    mounted.EndAnchor,
                    mounted.Owner);
                mounted.ActiveBranch = mounted.FallbackBranch;
            }
            else if (mounted.FallbackBranch is not null)
            {
                VirtualNode fallbackValue = EvaluateSlot(
                    next.Invocation,
                    "fallback",
                    mounted.Owner) ?? new CommentNode(string.Empty);
                mounted.FallbackBranch = Patch(
                    tree,
                    mounted.FallbackBranch,
                    fallbackValue,
                    container,
                    mounted.EndAnchor,
                    mounted.Owner);
                mounted.ActiveBranch = mounted.FallbackBranch;
            }
        }
        else
        {
            ResolveSuspense(tree, mounted, container);
        }

        _ = previous;
        ReplaceValue(tree, mounted, next);
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

        SchedulerJob job = new(
            () =>
            {
                mounted.ResolveJob = null;
                if (!mounted.IsUnmounted)
                {
                    ResolveSuspense(tree, mounted, container);
                }
            })
        {
            Name = "suspense reveal",
        };
        mounted.ResolveJob = job;
        Scheduler.QueuePostFlushCallback(job);
    }

    private void ResolveSuspense(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        TNode container)
    {
        if (mounted.Boundary.PendingCount > 0
            || ReferenceEquals(mounted.ActiveBranch, mounted.ContentBranch))
        {
            return;
        }

        if (mounted.FallbackBranch is not null)
        {
            Unmount(tree, mounted.FallbackBranch, removeHostNodes: true);
            mounted.FallbackBranch = null;
        }

        Move(mounted.ContentBranch, container, mounted.EndAnchor);
        mounted.ActiveBranch = mounted.ContentBranch;
        QueueHostCommit();
    }

    private void UnmountSuspense(
        MountedTree<TNode> tree,
        MountedSuspense<TNode> mounted,
        bool removeHostNodes)
    {
        if (mounted.ResolveJob is not null)
        {
            mounted.ResolveJob.IsDisposed = true;
            Scheduler.InvalidateJob(mounted.ResolveJob);
            mounted.ResolveJob = null;
        }

        mounted.Boundary.Dispose();
        if (mounted.FallbackBranch is not null)
        {
            Unmount(tree, mounted.FallbackBranch, removeHostNodes: false);
        }

        Unmount(
            tree,
            mounted.ContentBranch,
            removeHostNodes: !ReferenceEquals(
                mounted.ActiveBranch,
                mounted.ContentBranch));
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
        MountedNode<TNode> child = Mount(tree, childValue, container, anchor, owner);
        MountedTransition<TNode> mounted = new(value, child, owner)
        {
            State = TransitionExecutionState.Entered,
        };
        Register(tree, value, mounted);
        if (!ReadTransitionBoolean(value, "persisted")
            && ReadTransitionBoolean(value, "appear"))
        {
            BeginTransitionEnter(tree, mounted, child, value);
        }

        return mounted;
    }

    private void PatchTransition(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        TransitionNode next,
        TNode container)
    {
        TransitionNode previous = (TransitionNode)mounted.Value;
        SettleTransitionBeforePatch(tree, mounted);
        IReadOnlyList<KeyValuePair<object, TNode>> outgoingSnapshot =
            CaptureTransitionSnapshot(CurrentTransitionChild(mounted));
        ObserveTransitionBeforeUpdate(tree, mounted, outgoingSnapshot);
        VirtualNode nextValue = EvaluateSlot(next.Invocation, "default", mounted.Owner)
            ?? new CommentNode(string.Empty);
        MountedNode<TNode> current = CurrentTransitionChild(mounted);
        if (ReadTransitionBoolean(previous, "persisted")
            || ReadTransitionBoolean(next, "persisted")
            || IsSameNodeType(current.Value, nextValue))
        {
            mounted.Child = Patch(
                tree,
                current,
                nextValue,
                container,
                GetNextHostNode(current),
                mounted.Owner);
            mounted.IncomingChild = null;
            mounted.Overlap = null;
            mounted.State = TransitionExecutionState.Entered;
            ReplaceValue(tree, mounted, next);
            ObserveTransitionUpdated(
                tree,
                mounted,
                CaptureTransitionSnapshot(mounted.Child));
            return;
        }

        switch (ReadTransitionMode(next))
        {
            case TransitionMode.OutgoingThenIncoming:
                PatchTransitionOutgoingThenIncoming(
                    tree,
                    mounted,
                    previous,
                    next,
                    nextValue,
                    container);
                break;
            case TransitionMode.IncomingThenOutgoing:
                PatchTransitionWithOverlap(
                    tree,
                    mounted,
                    previous,
                    next,
                    nextValue,
                    container,
                    delayLeaveUntilEnter: true);
                break;
            default:
                PatchTransitionWithOverlap(
                    tree,
                    mounted,
                    previous,
                    next,
                    nextValue,
                    container,
                    delayLeaveUntilEnter: false);
                break;
        }
    }

    private void PatchTransitionOutgoingThenIncoming(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        TransitionNode previous,
        TransitionNode next,
        VirtualNode nextValue,
        TNode container)
    {
        MountedNode<TNode> outgoing = mounted.Child;
        TNode? insertionAnchor = GetNextHostNode(outgoing);
        ReplaceValue(tree, mounted, next);
        BeginTransitionLeave(
            tree,
            mounted,
            outgoing,
            previous,
            cancelled =>
            {
                if (mounted.IsUnmounted || !ReferenceEquals(mounted.Child, outgoing))
                {
                    return;
                }

                Unmount(tree, outgoing, removeHostNodes: true);
                MountedNode<TNode> incoming = Mount(
                    tree,
                    nextValue,
                    container,
                    insertionAnchor,
                    mounted.Owner);
                mounted.Child = incoming;
                mounted.State = TransitionExecutionState.Entered;
                if (!cancelled)
                {
                    BeginTransitionEnter(tree, mounted, incoming, next);
                }

                ObserveTransitionUpdated(
                    tree,
                    mounted,
                    CaptureTransitionSnapshot(incoming));
            });
    }

    private void PatchTransitionWithOverlap(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        TransitionNode previous,
        TransitionNode next,
        VirtualNode nextValue,
        TNode container,
        bool delayLeaveUntilEnter)
    {
        MountedNode<TNode> outgoing = mounted.Child;
        TNode? insertionAnchor = GetNextHostNode(outgoing);
        TNode startAnchor = _options.CreateComment("transition overlap start");
        TNode endAnchor = _options.CreateComment("transition overlap end");
        _options.Insert(startAnchor, container, outgoing.FirstHostNode);
        _options.Insert(endAnchor, container, insertionAnchor);
        MountedNode<TNode> incoming = Mount(
            tree,
            nextValue,
            container,
            endAnchor,
            mounted.Owner);
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
        ReplaceValue(tree, mounted, next);
        ObserveTransitionUpdated(
            tree,
            mounted,
            CaptureTransitionSnapshot(incoming));

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
                previous,
                cancelled => CompleteTransitionOverlap(
                    tree,
                    mounted,
                    overlap,
                    outgoing,
                    incoming,
                    cancelled));
        }

        BeginTransitionEnter(
            tree,
            mounted,
            incoming,
            next,
            delayLeaveUntilEnter ? BeginLeaveAfterEnter : null);
        if (!delayLeaveUntilEnter)
        {
            BeginLeaveAfterEnter(false);
        }
    }

    private void CompleteTransitionOverlap(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        MountedRange<TNode> overlap,
        MountedNode<TNode> outgoing,
        MountedNode<TNode> incoming,
        bool cancelled)
    {
        _ = cancelled;
        if (mounted.IsUnmounted || !ReferenceEquals(mounted.Overlap, overlap))
        {
            return;
        }

        Unmount(tree, outgoing, removeHostNodes: true);
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
        CancelTransitionOperationForUnmount(
            tree,
            mounted,
            mounted.EnterOperation,
            isEnter: true);
        CancelTransitionOperationForUnmount(
            tree,
            mounted,
            mounted.LeaveOperation,
            isEnter: false);
        Unmount(tree, mounted.Child, removeHostNodes);
        mounted.IncomingChild = null;
        mounted.Overlap = null;
        mounted.State = TransitionExecutionState.Left;
    }

    private void CancelTransitionOperationForUnmount(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        TransitionOperation<TNode>? operation,
        bool isEnter)
    {
        if (operation is null)
        {
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

        _ = tree;
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
        CompleteTransitionOperation(
            mounted,
            operation,
            cancelled: false);
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
        CompleteTransitionOperation(
            mounted,
            operation,
            cancelled: true);
    }

    private void BeginTransitionEnter(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        MountedNode<TNode> child,
        TransitionNode transition,
        Action<bool>? afterCompletion = null)
    {
        _ = tree;
        _ = child;
        _ = transition;
        mounted.State = TransitionExecutionState.Entered;
        afterCompletion?.Invoke(false);
    }

    private void BeginTransitionLeave(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        MountedNode<TNode> child,
        TransitionNode transition,
        Action<bool> afterCompletion)
    {
        _ = tree;
        _ = mounted;
        _ = child;
        _ = transition;
        afterCompletion(false);
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
        CompleteTransitionOperation(
            mounted,
            operation,
            cancelled: true);
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

    private void ObserveTransitionBeforeUpdate(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        IReadOnlyList<KeyValuePair<object, TNode>> outgoing)
    {
        _ = tree;
        _ = mounted;
        _ = outgoing;
    }

    private void ObserveTransitionUpdated(
        MountedTree<TNode> tree,
        MountedTransition<TNode> mounted,
        IReadOnlyList<KeyValuePair<object, TNode>> incoming)
    {
        _ = tree;
        _ = mounted;
        _ = incoming;
    }

    private static IReadOnlyList<KeyValuePair<object, TNode>> CaptureTransitionSnapshot(
        MountedNode<TNode> mounted)
    {
        List<KeyValuePair<object, TNode>> snapshot = [];
        if (mounted is MountedRange<TNode> range
            && mounted.Value is FragmentNode)
        {
            for (int index = 0; index < range.Children.Count; index++)
            {
                AddTransitionSnapshotEntry(range.Children[index], snapshot);
            }
        }
        else
        {
            AddTransitionSnapshotEntry(mounted, snapshot);
        }

        return snapshot.AsReadOnly();
    }

    private static void AddTransitionSnapshotEntry(
        MountedNode<TNode> mounted,
        List<KeyValuePair<object, TNode>> snapshot)
    {
        if (mounted.Value.Key is { } key
            && TryGetFirstTransitionElement(mounted, out TNode element))
        {
            snapshot.Add(new KeyValuePair<object, TNode>(key, element));
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

    private void InvokeTransitionHostCallback(
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
        }
    }

    private static MountedNode<TNode> CurrentTransitionChild(
        MountedTransition<TNode> mounted) =>
        mounted.IncomingChild ?? mounted.Child;

    private static bool ReadTransitionBoolean(
        TransitionNode transition,
        string name) =>
        transition.Invocation.Arguments.TryGetValue(name, out object? value)
        && value is true;

    private static TransitionMode ReadTransitionMode(TransitionNode transition)
    {
        if (!transition.Invocation.Arguments.TryGetValue("mode", out object? value)
            || value is not string mode)
        {
            return TransitionMode.Simultaneous;
        }

        return mode switch
        {
            "out-in" => TransitionMode.OutgoingThenIncoming,
            "in-out" => TransitionMode.IncomingThenOutgoing,
            _ => TransitionMode.Simultaneous,
        };
    }

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
