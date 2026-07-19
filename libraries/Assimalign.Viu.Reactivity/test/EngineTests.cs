using System;
using System.Runtime.CompilerServices;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class EngineTests
{
    [Fact]
    public void ReReadingTheSameDepInOneRunReusesTheLink()
    {
        var count = Reactive.Reference(1);
        var effect = new ReactiveEffect(() =>
        {
            _ = count.Value;
            _ = count.Value; // second read of the same dependency in the same run
        });
        effect.Run();

        // Exactly one link node for the dependency.
        effect.Dependencies.ShouldNotBeNull();
        effect.Dependencies.ShouldBeSameAs(effect.DependenciesTail);
        effect.Dependencies!.NextDependency.ShouldBeNull();
        var originalLink = effect.Dependencies;

        // Re-run (via trigger): the link node is reused, not reallocated.
        count.Value = 2;
        effect.Dependencies.ShouldBeSameAs(originalLink);
        effect.Dependencies.ShouldBeSameAs(effect.DependenciesTail);
    }

    [Fact]
    public void TriggerBumpsDepVersionAndGlobalVersion()
    {
        var dependency = new Dependency();
        dependency.Version.ShouldBe(0);
        var globalBefore = ReactivityState.GlobalVersion;

        dependency.Trigger();

        dependency.Version.ShouldBe(1);
        ReactivityState.GlobalVersion.ShouldBe(globalBefore + 1);
    }

    [Fact]
    public void StaleBranchLinksAreUnlinkedFromTheDepSubscriberList()
    {
        var flag = Reactive.Reference(true);
        var a = Reactive.Reference(1);
        var b = Reactive.Reference(10);
        Reactive.Effect(() => _ = flag.Value ? a.Value : b.Value);

        var aDependency = ((ITrackedReference)a).Dependency;
        var bDependency = ((ITrackedReference)b).Dependency;
        aDependency.Subscribers.ShouldNotBeNull();
        bDependency.Subscribers.ShouldBeNull();

        flag.Value = false;

        // The untaken branch was fully unlinked from the dependency side.
        aDependency.Subscribers.ShouldBeNull();
        bDependency.Subscribers.ShouldNotBeNull();
    }

    [Fact]
    public void TargetTrackingTracksAndTriggersByObjectAndKey()
    {
        var target = new object();
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            TargetTracking.Track(target, "name");
        });
        runs.ShouldBe(1);

        TargetTracking.Trigger(target, "name");
        runs.ShouldBe(2);

        TargetTracking.Trigger(target, "other");
        runs.ShouldBe(2);
    }

    [Fact]
    public void StoppingTheLastSubscriberSoftDetachesAComputedAndAReadReattaches()
    {
        var count = Reactive.Reference(1);
        var sourceDependency = ((ITrackedReference)count).Dependency;
        var getterRuns = 0;
        var doubled = Reactive.Computed(() =>
        {
            getterRuns++;
            return count.Value * 2;
        });
        var effect = Reactive.Effect(() => _ = doubled.Value);
        getterRuns.ShouldBe(1);
        sourceDependency.Subscribers.ShouldNotBeNull(); // the computed is linked into the source's sub list

        // Stopping the computed's only subscriber soft-unsubscribes it from its sources:
        // a source write no longer reaches the computed (the getter does not re-run).
        effect.Stop();
        sourceDependency.Subscribers.ShouldBeNull();
        count.Value = 2;
        getterRuns.ShouldBe(1);

        // A later direct read re-attaches and serves the fresh value — the getter run count
        // increments exactly on the read.
        doubled.Value.ShouldBe(4);
        getterRuns.ShouldBe(2);

        // And a new subscriber restores full reactivity end to end.
        var rerunSeen = 0;
        Reactive.Effect(() => rerunSeen = doubled.Value);
        rerunSeen.ShouldBe(4);
        sourceDependency.Subscribers.ShouldNotBeNull();

        count.Value = 3;
        rerunSeen.ShouldBe(6);
        getterRuns.ShouldBe(3);
    }

    [Fact]
    public void TriggerOnUntrackedKeyOfTrackedTargetLeavesGlobalVersionUnchanged()
    {
        var target = new object();
        Reactive.Effect(() => TargetTracking.Track(target, "name"));

        // Untracked KEY of a tracked target: nothing observes it — no global bump (Vue parity).
        var before = ReactivityState.GlobalVersion;
        TargetTracking.Trigger(target, "other");
        ReactivityState.GlobalVersion.ShouldBe(before);

        // The tracked key still triggers normally (and bumps the global version).
        TargetTracking.Trigger(target, "name");
        ReactivityState.GlobalVersion.ShouldBe(before + 1);
    }

    [Fact]
    public void TriggerOnNeverTrackedTargetBumpsGlobalVersion()
    {
        var target = new object();
        var before = ReactivityState.GlobalVersion;

        TargetTracking.Trigger(target, "any");

        ReactivityState.GlobalVersion.ShouldBe(before + 1);
    }

    [Fact]
    public void DroppedTargetIsCollectedWhileFormerSubscriberStaysAlive()
    {
        var (weakTarget, effect) = TrackThenDropTarget();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        weakTarget.IsAlive.ShouldBeFalse();
        effect.IsActive.ShouldBeTrue();
        GC.KeepAlive(effect);
    }

    private sealed class TargetHolder
    {
        public object? Target = new();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference WeakTarget, ReactiveEffect Effect) TrackThenDropTarget()
    {
        var holder = new TargetHolder();
        var weakTarget = new WeakReference(holder.Target);
        var effect = Reactive.Effect(() =>
        {
            var target = holder.Target;
            if (target is not null)
            {
                TargetTracking.Track(target, "key");
            }
        });

        // Drop the only strong reference; the ConditionalWeakTable key must not root it.
        holder.Target = null;
        return (weakTarget, effect);
    }
}
