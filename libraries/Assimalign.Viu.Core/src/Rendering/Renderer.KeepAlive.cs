using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private MountedKeepAliveState<TNode>? CreateKeepAliveState(
        MountedComponent instance)
    {
        return instance.Template is KeepAlive
            ? new MountedKeepAliveState<TNode>(
                _options.CreateElement("div", null))
            : null;
    }

    private void InitializeKeepAlive(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        MountedComponent instance,
        MountedRenderNode<TNode> child)
    {
        state.ActiveNode = child;
        if (!TryGetKeepAliveKey(child.Component, out object key)
            || !ShouldKeepAlive(instance, child, out string? componentName))
        {
            return;
        }

        state.ActiveKey = key;
        state.ActiveIsCached = true;
        state.Add(key, child, componentName);
        EnforceKeepAliveMaximum(tree, state, instance, child);
        QueueKeepAliveLifecycle(
            tree,
            child,
            activate: true,
            invokeComponentNodeHook: false,
            () => ReferenceEquals(state.ActiveNode, child)
                && state.ActiveIsCached);
    }

    private MountedRenderNode<TNode> PatchKeepAlive(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        MountedComponent instance,
        MountedRenderNode<TNode> current,
        IComponent next,
        TNode container,
        TNode? anchor,
        string? elementNamespace)
    {
        PruneExcludedKeepAliveEntries(tree, state, instance, current);

        if (IsSameComponentType(current.Component, next))
        {
            MountedRenderNode<TNode> patched = Patch(
                tree,
                current,
                next,
                container,
                anchor,
                elementNamespace,
                instance.Context);
            state.ActiveNode = patched;
            UpdateActiveKeepAliveEntry(
                tree,
                state,
                instance,
                patched);
            return patched;
        }

        KeepAliveCacheEntry<TNode>? outgoingEntry = null;
        string? outgoingName = null;
        bool shouldDeactivate =
            state.ActiveIsCached
            && state.ActiveKey is { } outgoingKey
            && state.Cache.TryGetValue(
                outgoingKey,
                out outgoingEntry)
            && ReferenceEquals(outgoingEntry.Node, current)
            && ShouldKeepAlive(
                instance,
                current,
                out outgoingName);
        if (shouldDeactivate)
        {
            outgoingEntry!.ComponentName = outgoingName;
            Move(current, state.StorageContainer, default);
            QueueKeepAliveLifecycle(
                tree,
                current,
                activate: false,
                invokeComponentNodeHook: true,
                () => !ReferenceEquals(state.ActiveNode, current));
        }
        else
        {
            if (state.ActiveKey is { } uncachedKey
                && state.Cache.TryGetValue(
                    uncachedKey,
                    out KeepAliveCacheEntry<TNode>? activeEntry)
                && ReferenceEquals(activeEntry.Node, current))
            {
                state.Remove(uncachedKey);
            }

            Unmount(tree, current, removeHostNodes: true);
        }

        state.ActiveNode = null;
        state.ActiveKey = null;
        state.ActiveIsCached = false;

        if (TryGetKeepAliveKey(next, out object incomingKey)
            && state.Cache.TryGetValue(
                incomingKey,
                out KeepAliveCacheEntry<TNode>? cached))
        {
            if (IsSameComponentType(cached.Node.Component, next))
            {
                MountedRenderNode<TNode> activated = Patch(
                    tree,
                    cached.Node,
                    next,
                    state.StorageContainer,
                    default,
                    elementNamespace,
                    instance.Context);
                cached.Node = activated;
                cached.ComponentName = ComponentName(activated);
                BeginKeepAliveEnter(activated);
                Move(activated, container, anchor);
                state.ActiveNode = activated;
                state.ActiveKey = incomingKey;
                state.ActiveIsCached = true;
                state.Touch(incomingKey);
                QueueKeepAliveActivation(tree, state, activated);
                return activated;
            }

            PruneKeepAliveEntry(
                tree,
                state,
                incomingKey,
                active: null);
        }

        MountedRenderNode<TNode> mounted = Mount(
            tree,
            next,
            container,
            anchor,
            elementNamespace,
            instance.Context);
        state.ActiveNode = mounted;
        if (ShouldKeepAlive(
                instance,
                mounted,
                out string? incomingName))
        {
            state.ActiveKey = incomingKey;
            state.ActiveIsCached = true;
            state.Add(incomingKey, mounted, incomingName);
            EnforceKeepAliveMaximum(
                tree,
                state,
                instance,
                mounted);
            QueueKeepAliveLifecycle(
                tree,
                mounted,
                activate: true,
                invokeComponentNodeHook: false,
                () => ReferenceEquals(state.ActiveNode, mounted)
                    && state.ActiveIsCached);
        }

        return mounted;
    }

    private void UpdateActiveKeepAliveEntry(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        MountedComponent instance,
        MountedRenderNode<TNode> active)
    {
        if (!TryGetKeepAliveKey(active.Component, out object key)
            || !ShouldKeepAlive(
                instance,
                active,
                out string? componentName))
        {
            if (state.ActiveKey is { } previousKey
                && state.Cache.TryGetValue(
                    previousKey,
                    out KeepAliveCacheEntry<TNode>? entry)
                && ReferenceEquals(entry.Node, active))
            {
                state.Remove(previousKey);
            }

            state.ActiveKey = null;
            state.ActiveIsCached = false;
            return;
        }

        if (state.ActiveIsCached
            && state.ActiveKey is { } activeKey
            && Equals(activeKey, key)
            && state.Cache.TryGetValue(
                activeKey,
                out KeepAliveCacheEntry<TNode>? activeEntry))
        {
            activeEntry.Node = active;
            activeEntry.ComponentName = componentName;
            state.Touch(activeKey);
        }
        else
        {
            if (state.ActiveKey is { } previousKey)
            {
                state.Remove(previousKey);
            }

            state.Add(key, active, componentName);
        }

        state.ActiveKey = key;
        state.ActiveIsCached = true;
        EnforceKeepAliveMaximum(tree, state, instance, active);
    }

    private void PruneExcludedKeepAliveEntries(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        MountedComponent instance,
        MountedRenderNode<TNode> active)
    {
        List<object> keys = new(state.Keys);
        for (int index = 0; index < keys.Count; index++)
        {
            object key = keys[index];
            if (!state.Cache.TryGetValue(
                key,
                out KeepAliveCacheEntry<TNode>? entry)
                || ((KeepAlive)instance.Template).ShouldCache(
                    instance.Context.Arguments,
                    entry.ComponentName))
            {
                continue;
            }

            PruneKeepAliveEntry(tree, state, key, active);
        }
    }

    private void EnforceKeepAliveMaximum(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        MountedComponent instance,
        MountedRenderNode<TNode> active)
    {
        int maximum =
            ((KeepAlive)instance.Template).Maximum(
                instance.Context.Arguments);
        if (maximum <= 0)
        {
            return;
        }

        while (state.Keys.Count > maximum)
        {
            PruneKeepAliveEntry(
                tree,
                state,
                state.Keys.First!.Value,
                active);
        }
    }

    private void PruneKeepAliveEntry(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        object key,
        MountedRenderNode<TNode>? active)
    {
        if (!state.Cache.TryGetValue(
            key,
            out KeepAliveCacheEntry<TNode>? entry))
        {
            return;
        }

        state.Remove(key);
        if (ReferenceEquals(entry.Node, active))
        {
            state.ActiveKey = null;
            state.ActiveIsCached = false;
            return;
        }

        Unmount(tree, entry.Node, removeHostNodes: true);
    }

    private void UnmountKeepAlive(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        MountedRenderNode<TNode> active,
        bool removeHostNodes)
    {
        if (state.ActiveIsCached)
        {
            InvokeKeepAliveLifecycle(active, activate: false);
        }

        List<MountedRenderNode<TNode>> cached = [];
        foreach (KeepAliveCacheEntry<TNode> entry in state.Cache.Values)
        {
            if (!ReferenceEquals(entry.Node, active))
            {
                cached.Add(entry.Node);
            }
        }

        state.Cache.Clear();
        state.Keys.Clear();
        state.KeyNodes.Clear();
        state.ActiveNode = null;
        state.ActiveKey = null;
        state.ActiveIsCached = false;

        for (int index = 0; index < cached.Count; index++)
        {
            Unmount(tree, cached[index], removeHostNodes: true);
        }

        Unmount(tree, active, removeHostNodes);
        _options.Remove(state.StorageContainer);
    }

    private static bool ShouldKeepAlive(
        MountedComponent keepAlive,
        MountedRenderNode<TNode> child,
        out string? componentName)
    {
        componentName = ComponentName(child);
        return child is MountedTemplateNode<TNode>
            && ((KeepAlive)keepAlive.Template).ShouldCache(
                keepAlive.Context.Arguments,
                componentName);
    }

    private static string? ComponentName(
        MountedRenderNode<TNode> child)
    {
        return child is MountedTemplateNode<TNode> template
            ? template.Instance.Template.Name
            : null;
    }

    private static bool TryGetKeepAliveKey(
        IComponent component,
        out object key)
    {
        if (component is not ITemplateComponent template)
        {
            key = null!;
            return false;
        }

        key = component.Key
            ?? (object?)template.TemplateType
            ?? template.TemplateName!;
        return key is not null;
    }

    private void QueueKeepAliveActivation(
        MountedTree<TNode> tree,
        MountedKeepAliveState<TNode> state,
        MountedRenderNode<TNode> child)
    {
        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    if (child.IsUnmounted
                        || !ReferenceEquals(state.ActiveNode, child)
                        || !state.ActiveIsCached)
                    {
                        return;
                    }

                    CompleteKeepAliveEnter(child);
                    InvokeKeepAliveLifecycle(child, activate: true);
                    InvokeComponentNodeLifecycleHook(
                        tree,
                        child.Owner,
                        child.Component,
                        previousComponent: null,
                        "onVnodeMounted");
                    QueueHostCommit();
                })
            {
                Name = "keep-alive activated lifecycle",
            });
    }

    private void QueueKeepAliveLifecycle(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode> child,
        bool activate,
        bool invokeComponentNodeHook,
        Func<bool> shouldInvoke)
    {
        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    if (child.IsUnmounted || !shouldInvoke())
                    {
                        return;
                    }

                    InvokeKeepAliveLifecycle(child, activate);
                    if (invokeComponentNodeHook)
                    {
                        InvokeComponentNodeLifecycleHook(
                            tree,
                            child.Owner,
                            child.Component,
                            previousComponent: null,
                            activate
                                ? "onVnodeMounted"
                                : "onVnodeUnmounted");
                    }
                })
            {
                Name = activate
                    ? "keep-alive activated lifecycle"
                    : "keep-alive deactivated lifecycle",
            });
    }

    private static void InvokeKeepAliveLifecycle(
        MountedRenderNode<TNode> child,
        bool activate)
    {
        switch (child)
        {
            case MountedTemplateNode<TNode> template:
                InvokeKeepAliveLifecycle(template.Subtree, activate);
                if (activate)
                {
                    template.Instance.InvokeActivated();
                }
                else
                {
                    template.Instance.InvokeDeactivated();
                }

                break;
            case MountedElementNode<TNode> element:
                InvokeKeepAliveLifecycle(element.Children, activate);
                break;
            case MountedFragmentNode<TNode> fragment:
                InvokeKeepAliveLifecycle(fragment.Children, activate);
                break;
            case MountedTeleportNode<TNode> teleport:
                InvokeKeepAliveLifecycle(teleport.Children, activate);
                break;
        }
    }

    private static void InvokeKeepAliveLifecycle(
        IReadOnlyList<MountedRenderNode<TNode>> children,
        bool activate)
    {
        for (int index = 0; index < children.Count; index++)
        {
            InvokeKeepAliveLifecycle(children[index], activate);
        }
    }

    private static void BeginKeepAliveEnter(
        MountedRenderNode<TNode> child)
    {
        switch (child)
        {
            case MountedTemplateNode<TNode> template:
                BeginKeepAliveEnter(template.Subtree);
                break;
            case MountedElementNode<TNode>
                {
                    Transition: { Persisted: false } transition,
                } element:
                transition.BeforeEnter(element.HostNode);
                break;
            case MountedFragmentNode<TNode> fragment:
                BeginKeepAliveEnter(fragment.Children);
                break;
        }
    }

    private static void BeginKeepAliveEnter(
        IReadOnlyList<MountedRenderNode<TNode>> children)
    {
        for (int index = 0; index < children.Count; index++)
        {
            BeginKeepAliveEnter(children[index]);
        }
    }

    private static void CompleteKeepAliveEnter(
        MountedRenderNode<TNode> child)
    {
        switch (child)
        {
            case MountedTemplateNode<TNode> template:
                CompleteKeepAliveEnter(template.Subtree);
                break;
            case MountedElementNode<TNode>
                {
                    Transition: { Persisted: false } transition,
                } element:
                transition.Enter(element.HostNode);
                break;
            case MountedFragmentNode<TNode> fragment:
                CompleteKeepAliveEnter(fragment.Children);
                break;
        }
    }

    private static void CompleteKeepAliveEnter(
        IReadOnlyList<MountedRenderNode<TNode>> children)
    {
        for (int index = 0; index < children.Count; index++)
        {
            CompleteKeepAliveEnter(children[index]);
        }
    }
}
