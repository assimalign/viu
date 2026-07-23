using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private static bool IsSuspenseComponent(
        ITemplateComponent component)
    {
        return component.TemplateType == typeof(Suspense)
            || string.Equals(
                component.TemplateName,
                Suspense.Registration.Name,
                StringComparison.Ordinal);
    }

    private MountedTemplateNode<TNode> MountSuspense(
        MountedTree<TNode> tree,
        ITemplateComponent component,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        IApplicationContext application = tree.Application
            ?? throw new InvalidOperationException(
                "Suspense requires an application context. Supply it to Render.");
        int identifier = checked(++_nextComponentIdentifier);
        MountedComponent instance = MountedComponent.Create(
            application,
            component,
            owner,
            identifier);
        Suspense suspense = instance.Template as Suspense
            ?? throw new InvalidOperationException(
                "The Suspense registration must activate a Suspense template.");
        SuspenseBoundary boundary = suspense.Boundary;
        MountedSuspenseState<TNode>? state = null;
        TNode? storageContainer = default;
        bool hasStorageContainer = false;
        MountedRenderNode<TNode>? subtree = null;
        MountedTemplateNode<TNode>? mounted = null;
        ReactiveEffect? renderEffect = null;
        SchedulerJob? renderJob = null;
        bool isInitialized = false;
        SchedulerJob mountedJob = new(instance.InvokeMounted)
        {
            Name = "Suspense mounted lifecycle",
        };
        SchedulerJob updatedJob = new(instance.InvokeUpdated)
        {
            Name = "Suspense updated lifecycle",
        };

        try
        {
            void RenderBoundary()
            {
                if (state is null)
                {
                    instance.InvokeBeforeMount();
                    InvokeComponentNodeLifecycleHook(
                        tree,
                        owner,
                        component,
                        previousComponent: null,
                        "onVnodeBeforeMount");
                    storageContainer = _options.CreateElement(
                        "div",
                        null);
                    hasStorageContainer = true;
                    MountedRenderNode<TNode> contentBranch = Mount(
                        tree,
                        instance.Render(),
                        storageContainer,
                        default,
                        elementNamespace,
                        instance.Context);
                    state = new MountedSuspenseState<TNode>(
                        storageContainer,
                        boundary,
                        contentBranch);
                    if (boundary.IsPending)
                    {
                        state.FallbackBranch = suspense.RunWithParentBoundary(
                            () => Mount(
                                tree,
                                suspense.RenderFallback(),
                                container,
                                anchor,
                                elementNamespace,
                                instance.Context));
                        state.IsShowingFallback = true;
                        subtree = state.FallbackBranch;
                    }
                    else
                    {
                        Move(contentBranch, container, anchor);
                        subtree = contentBranch;
                    }

                    QueueHostCommit();
                    return;
                }

                instance.InvokeBeforeUpdate();
                InvokePendingTemplateNodeBeforeUpdateHook(
                    tree,
                    mounted);
                TNode currentFallbackContainer = mounted is null
                    ? container
                    : mounted.FallbackContainer;
                string? currentElementNamespace = mounted is null
                    ? elementNamespace
                    : mounted.ElementNamespace;
                PatchSuspenseContent(
                    tree,
                    instance,
                    state,
                    currentFallbackContainer,
                    currentElementNamespace);
                ReconcileSuspensePresentation(
                    tree,
                    instance,
                    suspense,
                    state,
                    currentFallbackContainer,
                    currentElementNamespace);
                subtree = state.IsShowingFallback
                    ? state.FallbackBranch!
                    : state.ContentBranch;
                if (mounted is not null)
                {
                    mounted.Subtree = subtree;
                }

                QueueHostCommit();
                Scheduler.QueuePostFlushCallback(updatedJob);
            }

            renderEffect = instance.CreateRenderEffect(
                RenderBoundary,
                () => Scheduler.QueueJob(renderJob!));
            renderJob = new SchedulerJob(renderEffect.Run)
            {
                Identifier = identifier,
                Name = "Suspense render",
            };
            boundary.BindUpdateScheduler(
                () =>
                {
                    if (isInitialized
                        && renderJob is { IsDisposed: false } activeJob)
                    {
                        Scheduler.QueueJob(activeJob);
                    }
                });
            renderEffect.Run();

            mounted = new MountedTemplateNode<TNode>(
                component,
                instance,
                subtree!,
                renderEffect,
                renderJob,
                mountedJob,
                updatedJob,
                container,
                elementNamespace,
                owner)
            {
                SuspenseState = state,
            };
            instance.Context.RootElementResolver =
                () => GetRootElementObjects(mounted.Subtree);
            instance.Context.KeyedChildElementResolver =
                () => GetKeyedChildElementSnapshots(mounted.Subtree);
            instance.Context.HostCommitScheduler = QueueHostCommit;
            Register(tree, component, mounted);
            UpdateReference(
                tree,
                mounted,
                previousReference: null,
                component.Reference,
                ComponentReferenceValue(instance.Context));
            Scheduler.QueuePostFlushCallback(mountedJob);
            QueueComponentNodeLifecycleHook(
                tree,
                owner,
                mounted,
                component,
                previousComponent: null,
                "onVnodeMounted");
            isInitialized = true;
            if (state!.IsShowingFallback == !boundary.IsPending)
            {
                Scheduler.QueueJob(renderJob);
            }

            return mounted;
        }
        catch
        {
            renderJob?.IsDisposed = true;
            mountedJob.IsDisposed = true;
            updatedJob.IsDisposed = true;
            boundary.Dispose();
            try
            {
                instance.AbortMount(
                    state is null
                        ? null
                        : () => UnmountSuspenseStorage(
                            tree,
                            state,
                            removeVisibleHostNodes: true));
            }
            finally
            {
                if (state is null && hasStorageContainer)
                {
                    _options.Remove(storageContainer!);
                }
            }

            throw;
        }
    }

    private void PatchSuspense(
        MountedTree<TNode> tree,
        MountedTemplateNode<TNode> mounted,
        ITemplateComponent next,
        TNode container,
        string? elementNamespace)
    {
        ITemplateComponent current = RequireTemplate(mounted.Component);
        mounted.FallbackContainer = container;
        mounted.ElementNamespace = elementNamespace;
        mounted.Transition = TransitionComponents.Get(next);
        bool shouldUpdate = ShouldUpdateTemplate(mounted, current, next);
        if (shouldUpdate)
        {
            mounted.PendingNodeLifecycleComponent = next;
            mounted.PreviousNodeLifecycleComponent = current;
            mounted.Instance.Update(next);
            Scheduler.InvalidateJob(mounted.RenderJob);
            mounted.RenderEffect.Run();
            QueueComponentNodeLifecycleHook(
                tree,
                mounted.Owner,
                mounted,
                next,
                current,
                "onVnodeUpdated");
        }
        else
        {
            mounted.Instance.UpdateRequest(next);
        }

        UpdateReference(
            tree,
            mounted,
            current.Reference,
            next.Reference,
            ComponentReferenceValue(mounted.Instance.Context));
        ReplaceRegistration(tree, mounted, next);
    }

    private void PatchSuspenseContent(
        MountedTree<TNode> tree,
        MountedComponent instance,
        MountedSuspenseState<TNode> state,
        TNode fallbackContainer,
        string? elementNamespace)
    {
        TNode contentContainer;
        TNode? contentAnchor;
        if (state.IsShowingFallback)
        {
            contentContainer = state.StorageContainer;
            contentAnchor = default;
        }
        else
        {
            contentContainer = HostParentOrFallback(
                state.ContentBranch.FirstHostNode,
                fallbackContainer);
            contentAnchor = GetNextHostNode(state.ContentBranch);
        }

        state.ContentBranch = Patch(
            tree,
            state.ContentBranch,
            instance.Render(),
            contentContainer,
            contentAnchor,
            elementNamespace,
            instance.Context);
    }

    private void ReconcileSuspensePresentation(
        MountedTree<TNode> tree,
        MountedComponent instance,
        Suspense suspense,
        MountedSuspenseState<TNode> state,
        TNode fallbackContainer,
        string? elementNamespace)
    {
        if (state.Boundary.IsPending)
        {
            if (state.IsShowingFallback)
            {
                MountedRenderNode<TNode> fallback = state.FallbackBranch!;
                TNode currentContainer = HostParentOrFallback(
                    fallback.FirstHostNode,
                    fallbackContainer);
                TNode? currentAnchor = GetNextHostNode(fallback);
                state.FallbackBranch = suspense.RunWithParentBoundary(
                    () => Patch(
                        tree,
                        fallback,
                        suspense.RenderFallback(),
                        currentContainer,
                        currentAnchor,
                        elementNamespace,
                        instance.Context));
                return;
            }

            TNode visibleContainer = HostParentOrFallback(
                state.ContentBranch.FirstHostNode,
                fallbackContainer);
            TNode? visibleAnchor = GetNextHostNode(state.ContentBranch);
            Move(
                state.ContentBranch,
                state.StorageContainer,
                anchor: default);
            state.FallbackBranch = suspense.RunWithParentBoundary(
                () => Mount(
                    tree,
                    suspense.RenderFallback(),
                    visibleContainer,
                    visibleAnchor,
                    elementNamespace,
                    instance.Context));
            state.IsShowingFallback = true;
            return;
        }

        if (!state.IsShowingFallback)
        {
            return;
        }

        MountedRenderNode<TNode> currentFallback = state.FallbackBranch!;
        TNode revealContainer = HostParentOrFallback(
            currentFallback.FirstHostNode,
            fallbackContainer);
        TNode? revealAnchor = GetNextHostNode(currentFallback);
        Unmount(
            tree,
            currentFallback,
            removeHostNodes: true);
        Move(
            state.ContentBranch,
            revealContainer,
            revealAnchor);
        state.FallbackBranch = null;
        state.IsShowingFallback = false;
    }

    private void UnmountSuspense(
        MountedTree<TNode> tree,
        MountedTemplateNode<TNode> mounted,
        bool removeHostNodes)
    {
        MountedSuspenseState<TNode> state = mounted.SuspenseState!;
        mounted.RenderJob.IsDisposed = true;
        mounted.MountedJob.IsDisposed = true;
        mounted.UpdatedJob.IsDisposed = true;
        Scheduler.InvalidateJob(mounted.RenderJob);
        state.Boundary.Dispose();
        mounted.Instance.Unmount(
            () => UnmountSuspenseStorage(
                tree,
                state,
                removeHostNodes));
    }

    private void UnmountSuspenseStorage(
        MountedTree<TNode> tree,
        MountedSuspenseState<TNode> state,
        bool removeVisibleHostNodes)
    {
        try
        {
            UnmountSuspenseBranches(
                tree,
                state,
                removeVisibleHostNodes);
        }
        finally
        {
            _options.Remove(state.StorageContainer);
        }
    }

    private void UnmountSuspenseBranches(
        MountedTree<TNode> tree,
        MountedSuspenseState<TNode> state,
        bool removeVisibleHostNodes)
    {
        if (state.IsShowingFallback)
        {
            Unmount(
                tree,
                state.FallbackBranch!,
                removeVisibleHostNodes);
            Unmount(
                tree,
                state.ContentBranch,
                removeHostNodes: true);
            return;
        }

        Unmount(
            tree,
            state.ContentBranch,
            removeVisibleHostNodes);
    }
}
