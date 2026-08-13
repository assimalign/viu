using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Contains deferred hydration lifetime handling for <see cref="Renderer{TNode}"/>.</summary>
public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private void PatchLazyHydration(
        MountedTree<TNode> tree,
        MountedLazyHydration<TNode> mounted,
        ComponentNode next,
        TNode container)
    {
        if (mounted.ActivatedComponent is { } activated)
        {
            PatchComponent(tree, activated, next, container);
            ReplaceValue(tree, mounted, next);
            return;
        }

        ComponentNode previous = (ComponentNode)mounted.Value;
        ReplaceValue(tree, mounted, next);
        if (mounted.PendingActivation is { } pendingActivation)
        {
            pendingActivation.Update(next);
            return;
        }

        HydrationStrategy? nextStrategy = next.Invocation.HydrationStrategy;
        if (nextStrategy is null
            || nextStrategy.Kind == HydrationStrategyKind.Immediate)
        {
            mounted.TriggerRegistration?.Dispose();
            mounted.TriggerRegistration = null;
            ActivateLazyHydration(tree, mounted);
            return;
        }

        HydrationStrategy? previousStrategy = previous.Invocation.HydrationStrategy;
        if (HasSameHydrationTrigger(previousStrategy, nextStrategy))
        {
            return;
        }

        mounted.TriggerRegistration?.Dispose();
        mounted.TriggerRegistration = null;
        ScheduleLazyHydrationTrigger(tree, mounted, nextStrategy);
    }

    private static bool HasSameHydrationTrigger(
        HydrationStrategy? previous,
        HydrationStrategy next)
    {
        if (previous is null
            || previous.Kind != next.Kind
            || previous.IdleTimeoutMilliseconds != next.IdleTimeoutMilliseconds
            || !string.Equals(
                previous.VisibilityRootMargin,
                next.VisibilityRootMargin,
                StringComparison.Ordinal)
            || !string.Equals(
                previous.MediaQuery,
                next.MediaQuery,
                StringComparison.Ordinal)
            || previous.InteractionEvents.Count != next.InteractionEvents.Count)
        {
            return false;
        }

        for (int index = 0; index < previous.InteractionEvents.Count; index++)
        {
            if (!string.Equals(
                previous.InteractionEvents[index],
                next.InteractionEvents[index],
                StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void ScheduleLazyHydrationTrigger(
        MountedTree<TNode> tree,
        MountedLazyHydration<TNode> mounted,
        HydrationStrategy strategy)
    {
        Func<HydrationTriggerRequest<TNode>, IHydrationTriggerRegistration> schedule =
            _options.ScheduleHydrationTrigger
            ?? throw new NotSupportedException(
                "The active host does not provide deferred hydration triggers.");
        SchedulerJob activationJob = mounted.ActivationJob ?? new SchedulerJob(
            () => ActivateLazyHydration(tree, mounted))
        {
            Name = "lazy hydration activation",
        };
        mounted.ActivationJob = activationJob;
        mounted.TriggerRegistration = schedule(
            new HydrationTriggerRequest<TNode>(
                strategy,
                mounted.StartAnchor,
                mounted.EndAnchor,
                () =>
                {
                    if (!mounted.IsUnmounted && mounted.ActivatedComponent is null)
                    {
                        Scheduler.QueuePostFlushCallback(activationJob);
                    }
                }));
    }

    private void UnmountLazyHydration(
        MountedTree<TNode> tree,
        MountedLazyHydration<TNode> mounted,
        bool removeHostNodes)
    {
        if (mounted.ActivationJob is { } activationJob)
        {
            activationJob.IsDisposed = true;
        }

        mounted.ActivationJob = null;
        mounted.PendingActivation?.Release();
        mounted.PendingActivation = null;
        mounted.PendingComponentIdentifier = 0;
        mounted.TriggerRegistration?.Dispose();
        mounted.TriggerRegistration = null;
        if (mounted.ActivatedComponent is { } activated)
        {
            Unmount(tree, activated, removeHostNodes: false);
            mounted.ActivatedComponent = null;
        }

        if (removeHostNodes)
        {
            RemoveRange(mounted.StartAnchor, mounted.EndAnchor);
        }
    }
}
