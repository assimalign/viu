using System.Runtime.CompilerServices;
using Shouldly;
using Xunit;

namespace Assimalign.Vue.Reactivity.Tests;

public sealed class EngineTests
{
    [Fact]
    public void ReReadingTheSameDepInOneRunReusesTheLink()
    {
        var count = Reactive.Ref(1);
        var effect = new ReactiveEffect(() =>
        {
            _ = count.Value;
            _ = count.Value; // second read of the same dep in the same run
        });
        effect.Run();

        // Exactly one link node for the dep.
        effect.Deps.ShouldNotBeNull();
        effect.Deps.ShouldBeSameAs(effect.DepsTail);
        effect.Deps!.NextDep.ShouldBeNull();
        var originalLink = effect.Deps;

        // Re-run (via trigger): the link node is reused, not reallocated.
        count.Value = 2;
        effect.Deps.ShouldBeSameAs(originalLink);
        effect.Deps.ShouldBeSameAs(effect.DepsTail);
    }

    [Fact]
    public void TriggerBumpsDepVersionAndGlobalVersion()
    {
        var dep = new Dep();
        dep.Version.ShouldBe(0);
        var globalBefore = ReactivityState.GlobalVersion;

        dep.Trigger();

        dep.Version.ShouldBe(1);
        ReactivityState.GlobalVersion.ShouldBe(globalBefore + 1);
    }

    [Fact]
    public void StaleBranchLinksAreUnlinkedFromTheDepSubscriberList()
    {
        var flag = Reactive.Ref(true);
        var a = Reactive.Ref(1);
        var b = Reactive.Ref(10);
        Reactive.Effect(() => _ = flag.Value ? a.Value : b.Value);

        var aDep = ((ITrackedRef)a).Dep;
        var bDep = ((ITrackedRef)b).Dep;
        aDep.Subs.ShouldNotBeNull();
        bDep.Subs.ShouldBeNull();

        flag.Value = false;

        // The untaken branch was fully unlinked from the dep side.
        aDep.Subs.ShouldBeNull();
        bDep.Subs.ShouldNotBeNull();
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
        var count = Reactive.Ref(1);
        var sourceDep = ((ITrackedRef)count).Dep;
        var getterRuns = 0;
        var doubled = Reactive.Computed(() =>
        {
            getterRuns++;
            return count.Value * 2;
        });
        var effect = Reactive.Effect(() => _ = doubled.Value);
        getterRuns.ShouldBe(1);
        sourceDep.Subs.ShouldNotBeNull(); // the computed is linked into the source's sub list

        // Stopping the computed's only subscriber soft-unsubscribes it from its sources:
        // a source write no longer reaches the computed (the getter does not re-run).
        effect.Stop();
        sourceDep.Subs.ShouldBeNull();
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
        sourceDep.Subs.ShouldNotBeNull();

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
