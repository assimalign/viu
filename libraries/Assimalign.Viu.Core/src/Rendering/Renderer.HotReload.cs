using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private void ResetTemplateForHotReload(
        MountedTree<TNode> tree,
        MountedTemplateNode<TNode> mounted)
    {
        IApplicationContext application = tree.Application
            ?? throw new InvalidOperationException(
                "Hot Reload requires the mounted application context.");
        ITemplateComponent request = RequireTemplate(mounted.Component);
        ComponentContext? owner = mounted.Owner;
        int identifier = mounted.Instance.Context.Identifier;
        TNode container = HostParentOrFallback(
            mounted.Subtree.FirstHostNode,
            mounted.FallbackContainer);
        TNode? anchor = GetNextHostNode(mounted.Subtree);
        string? elementNamespace = mounted.ElementNamespace;
        IComponentReference? reference = OwnTemplateReference(
            mounted.Instance,
            request);

        mounted.RenderJob.IsDisposed = true;
        mounted.MountedJob.IsDisposed = true;
        mounted.UpdatedJob.IsDisposed = true;
        Scheduler.InvalidateJob(mounted.RenderJob);
        if (mounted.ReferenceJob is not null)
        {
            mounted.ReferenceJob.IsDisposed = true;
            mounted.ReferenceJob = null;
        }

        if (reference is not null)
        {
            InvokeReference(
                tree,
                owner,
                reference,
                value: null);
        }

        InvokeComponentNodeLifecycleHook(
            tree,
            owner,
            request,
            previousComponent: null,
            "onVnodeBeforeUnmount");

        MountedComponent previousInstance = mounted.Instance;
        MountedRenderNode<TNode> previousSubtree = mounted.Subtree;
        MountedKeepAliveState<TNode>? previousKeepAliveState =
            mounted.KeepAliveState;
        previousInstance.Unmount(
            () =>
            {
                if (previousKeepAliveState is not null)
                {
                    UnmountKeepAlive(
                        tree,
                        previousKeepAliveState,
                        previousSubtree,
                        removeHostNodes: true);
                }
                else
                {
                    Unmount(
                        tree,
                        previousSubtree,
                        removeHostNodes: true);
                }
            });
        QueueComponentNodeLifecycleHook(
            tree,
            owner,
            mounted: null,
            request,
            previousComponent: null,
            "onVnodeUnmounted");

        MountedComponent instance = MountedComponent.Create(
            application,
            request,
            owner,
            identifier);
        MountedKeepAliveState<TNode>? keepAliveState =
            CreateKeepAliveState(instance);
        MountedRenderNode<TNode>? subtree = null;
        ReactiveEffect? renderEffect = null;
        SchedulerJob? renderJob = null;
        SchedulerJob mountedJob = new(instance.InvokeMounted)
        {
            Name = "Hot Reload component mounted lifecycle",
        };
        SchedulerJob updatedJob = new(instance.InvokeUpdated)
        {
            Name = "Hot Reload component updated lifecycle",
        };

        try
        {
            IComponent RenderSubtree()
            {
                IComponent rendered = instance.Render();
                return mounted.Transition is null
                    ? rendered
                    : TransitionComponents.Attach(
                        rendered,
                        mounted.Transition);
            }

            void RenderComponent()
            {
                if (subtree is null)
                {
                    instance.InvokeBeforeMount();
                    InvokeComponentNodeLifecycleHook(
                        tree,
                        owner,
                        request,
                        previousComponent: null,
                        "onVnodeBeforeMount");
                    IComponent initialRendered = RenderSubtree();
                    subtree = Mount(
                        tree,
                        initialRendered,
                        container,
                        anchor,
                        elementNamespace,
                        instance.Context);
                    if (keepAliveState is not null)
                    {
                        InitializeKeepAlive(
                            tree,
                            keepAliveState,
                            instance,
                            subtree);
                    }

                    QueueHostCommit();
                    return;
                }

                instance.InvokeBeforeUpdate();
                InvokePendingTemplateNodeBeforeUpdateHook(
                    tree,
                    mounted);
                TNode updateContainer = HostParentOrFallback(
                    subtree.FirstHostNode,
                    mounted.FallbackContainer);
                TNode? updateAnchor = GetNextHostNode(subtree);
                IComponent rendered = RenderSubtree();
                subtree = keepAliveState is null
                    ? Patch(
                        tree,
                        subtree,
                        rendered,
                        updateContainer,
                        updateAnchor,
                        mounted.ElementNamespace,
                        instance.Context)
                    : PatchKeepAlive(
                        tree,
                        keepAliveState,
                        instance,
                        subtree,
                        rendered,
                        updateContainer,
                        updateAnchor,
                        mounted.ElementNamespace);
                mounted.Subtree = subtree;
                QueueHostCommit();
                Scheduler.QueuePostFlushCallback(updatedJob);
            }

            renderEffect = instance.CreateRenderEffect(
                RenderComponent,
                () => Scheduler.QueueJob(renderJob!));
            renderJob = new SchedulerJob(renderEffect.RunIfDirty)
            {
                Identifier = identifier,
                Name = "Hot Reload component render",
            };
            renderEffect.Run();

            mounted.Instance = instance;
            mounted.Subtree = subtree!;
            mounted.RenderEffect = renderEffect;
            mounted.RenderJob = renderJob;
            mounted.MountedJob = mountedJob;
            mounted.UpdatedJob = updatedJob;
            mounted.KeepAliveState = keepAliveState;
            mounted.PendingNodeLifecycleComponent = null;
            mounted.PreviousNodeLifecycleComponent = null;
            instance.Context.RootElementResolver =
                () => GetRootElementObjects(mounted.Subtree);
            instance.Context.KeyedChildElementResolver =
                () => GetKeyedChildElementSnapshots(mounted.Subtree);
            instance.Context.HostCommitScheduler = QueueHostCommit;
            instance.RegisterHotReload(
                () => ResetTemplateForHotReload(tree, mounted));
            UpdateReference(
                tree,
                mounted,
                previousReference: null,
                OwnTemplateReference(instance, request),
                ComponentReferenceValue(instance.Context));
            Scheduler.QueuePostFlushCallback(mountedJob);
            QueueComponentNodeLifecycleHook(
                tree,
                owner,
                mounted,
                request,
                previousComponent: null,
                "onVnodeMounted");
        }
        catch
        {
            renderJob?.IsDisposed = true;
            mountedJob.IsDisposed = true;
            updatedJob.IsDisposed = true;
            instance.AbortMount(
                subtree is null
                    ? null
                    : () => Unmount(
                        tree,
                        subtree,
                        removeHostNodes: true));
            if (keepAliveState is not null)
            {
                _options.Remove(keepAliveState.StorageContainer);
            }

            throw;
        }
    }
}
