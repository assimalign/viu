using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ReactivityInspectionTests
{
    private readonly ITestOutputHelper _output;

    public ReactivityInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DisabledInspection_SteadyStateWriteEffectLoop_AllocatesZeroBytes()
    {
        // [DVT-12]: with support enabled (RuntimeInspectionSupport) and no observer installed, the
        // guard is one field read and one null check and allocates nothing. The switch-disabled
        // path is the trimmer's constant substitution of IsSupported, not observable in-process.
        ReactivityInspection.IsSupported.ShouldBeTrue();
        ReactivityInspection.IsEnabled.ShouldBeFalse();
        var source = Reactive.Reference(0);
        int runs = 0;
        using var effect = Reactive.Effect(() => { _ = source.Value; runs++; });
        for (int iteration = 1; iteration <= 20_000; iteration++)
        {
            source.Value = iteration;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 20_001; iteration <= 120_000; iteration++)
        {
            source.Value = iteration;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        runs.ShouldBe(120_001);
        _output.WriteLine($"100000 writes/effect runs with no observer installed: {allocated} allocated bytes.");
    }

    [Fact]
    public void Inspection_ReferenceWrite_OrdersTriggerScheduleRunAndTracksWithoutExtraSubscriptions()
    {
        // [RCT-13], [DVT-8]: observation reads never become dependencies of the application effect.
        var source = Reactive.WithDebugLabel(Reactive.Reference(0), "count");
        var observedOnly = Reactive.Reference(0);
        var hook = new RecordingHook { OnObservation = () => { _ = observedOnly.Value; } };
        using var registration = ReactivityInspection.Use(hook);
        int runs = 0;
        using var effect = Reactive.Effect(() => { _ = source.Value; runs++; });
        hook.Events.Clear();

        source.Value = 1;

        hook.Events.ShouldBe(new[] { "trigger:count:write", "scheduled", "started", "track:count", "completed:True" });
        hook.LastCause.ShouldBeSameAs(source.Dependency);
        effect.FirstDependency!.Dependency.ShouldBeSameAs(source.Dependency);
        effect.FirstDependency.NextDependency.ShouldBeNull();
        observedOnly.Value = 1;
        runs.ShouldBe(2);
    }

    [Fact]
    public void Inspection_ReadingComputed_PreservesItsOwnDependenciesWithoutSubscribingTheObservedEffect()
    {
        // [RCT-13]: pausing ambient collection must not poison a computed first refreshed by a hook.
        var applicationSource = Reactive.Reference(0);
        var computedSource = Reactive.Reference(0);
        int getterRuns = 0;
        var computed = Reactive.Computed(() => { getterRuns++; return computedSource.Value * 2; });
        var hook = new RecordingHook { OnObservation = () => { _ = computed.Value; } };
        using var registration = ReactivityInspection.Use(hook);
        int applicationRuns = 0;
        using var applicationEffect = Reactive.Effect(() => { _ = applicationSource.Value; applicationRuns++; });
        int derivedRuns = 0;
        using var derivedEffect = Reactive.Effect(() => { _ = computed.Value; derivedRuns++; });

        computedSource.Value = 1;

        applicationRuns.ShouldBe(1);
        derivedRuns.ShouldBe(2);
        getterRuns.ShouldBe(2);
        computed.Value.ShouldBe(2);
        applicationEffect.FirstDependency!.Dependency.ShouldBeSameAs(applicationSource.Dependency);
        applicationEffect.FirstDependency.NextDependency.ShouldBeNull();
    }

    [Fact]
    public void Inspection_GeneratedPropertiesAndComputed_PreserveNamesAndWeakOwners()
    {
        // [DVT-9]: the compiled property literal and reference label are attribution, not reflection.
        var person = new ReactivePerson { Name = "Ada", Age = 30 };
        var hook = new RecordingHook { ExpectedOwner = person };
        using var registration = ReactivityInspection.Use(hook);
        var derived = Reactive.WithDebugLabel(Reactive.Computed(() => person.Age * 2), "double age");
        using var effect = Reactive.Effect(() => { _ = derived.Value; });

        person.Age = 31;

        hook.Events.ShouldContain("track:Age");
        hook.Events.ShouldContain("trigger:Age:write");
        hook.Events.ShouldContain("track:double age");
        hook.Events.ShouldContain("trigger:double age:propagation");
        hook.SawExpectedOwner.ShouldBeTrue();
        derived.Value.ShouldBe(62);
    }

    [Fact]
    public void Inspection_BatchedRepeatedWrites_SchedulesEffectOnceWithFirstCause()
    {
        var source = Reactive.Reference(0);
        var hook = new RecordingHook();
        using var registration = ReactivityInspection.Use(hook);
        int runs = 0;
        using var effect = Reactive.Effect(() => { _ = source.Value; runs++; });
        hook.Events.Clear();

        using (Reactive.Batch())
        {
            source.Value = 1;
            source.Value = 2;
        }

        hook.Events.Count(value => value == "scheduled").ShouldBe(1);
        hook.LastCause.ShouldBeSameAs(source.Dependency);
        runs.ShouldBe(2);
    }

    [Fact]
    public void Inspection_ThrowingHookAndEffect_RestoresTrackingAndReportsFailedCompletion()
    {
        var source = Reactive.Reference(0);
        var hook = new RecordingHook { OnObservation = () => throw new InvalidOperationException("observer") };
        using var registration = ReactivityInspection.Use(hook);
        int runs = 0;
        using var effect = Reactive.Effect(() =>
        {
            runs++;
            if (source.Value == 1)
            {
                throw new ArgumentException("application");
            }
        });

        Should.Throw<ArgumentException>(() => source.Value = 1).Message.ShouldBe("application");
        source.Value = 2;

        hook.Events.ShouldContain("completed:False");
        runs.ShouldBe(3);
        ReactivityState.ShouldTrack.ShouldBeTrue();
        ReactivityState.ActiveSubscriber.ShouldBeNull();
    }

    [Fact]
    public void Inspection_ExplicitStoppedEffectRun_ReportsSuccessfulAndFailedInvocationsWithoutResubscribing()
    {
        var source = Reactive.Reference(0);
        using var effect = Reactive.Effect(() =>
        {
            if (source.Value == 1)
            {
                throw new ArgumentException("stopped invocation");
            }
        });
        effect.Stop();
        var hook = new RecordingHook();
        using var registration = ReactivityInspection.Use(hook);

        effect.Run();
        hook.Events.ShouldBe(new[] { "started", "completed:True" });
        source.Value = 1;
        hook.Events.Clear();
        Should.Throw<ArgumentException>(() => effect.Run()).Message.ShouldBe("stopped invocation");

        hook.Events.ShouldBe(new[] { "started", "completed:False" });
        effect.IsActive.ShouldBeFalse();
        effect.FirstDependency.ShouldBeNull();
    }

    [Fact]
    public void Inspection_Attribution_DoesNotRetainOwnerWhileDependencyRemainsAlive()
    {
        using var registration = ReactivityInspection.Use(new RecordingHook());
        (Dependency dependency, WeakReference owner) = CreateObservedOwner();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        owner.IsAlive.ShouldBeFalse();
        GC.KeepAlive(dependency);
    }

    [Fact]
    public void Inspection_RetainedCustomReferenceTrigger_DoesNotExtendReferenceLifetime()
    {
        using var registration = ReactivityInspection.Use(new RecordingHook());
        (Action trigger, WeakReference reference) = CreateCustomReferenceTrigger();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        reference.IsAlive.ShouldBeFalse();
        trigger();
        GC.KeepAlive(trigger);
    }

    [Fact]
    public void Inspection_CustomReferenceCreatedBeforeRegistration_PreservesLabelAcrossRegistrations()
    {
        int value = 0;
        var reference = Reactive.WithDebugLabel(Reactive.CustomReference<int>((track, trigger) =>
            (() => { track(); return value; }, next => { value = next; trigger(); })), "custom count");
        using (ReactivityInspection.Use(new RecordingHook()))
        {
            reference.Value = 1;
        }
        var hook = new RecordingHook();
        using var registration = ReactivityInspection.Use(hook);

        reference.Value = 2;

        hook.Events.ShouldContain("trigger:custom count:write");
    }

    [Fact]
    public void WithDebugLabel_PreservesIdentityAndRejectsReinitialization()
    {
        var source = Reactive.Reference(0);
        Reactive.WithDebugLabel(source, "count").ShouldBeSameAs(source);
        source.DebugLabel.ShouldBe("count");
        Should.Throw<InvalidOperationException>(() => Reactive.WithDebugLabel(source, "again"));
        Should.Throw<ArgumentException>(() => Reactive.WithDebugLabel(Reactive.Reference(0), " "));
    }

    [Fact]
    public void Use_RequiresExclusiveOwnership_AndDisposalIsIdempotent()
    {
        // [DVT-8]: one hook per process; the support precondition is the linker-configured switch.
        var registration = ReactivityInspection.Use(new RecordingHook());
        try
        {
            Should.Throw<InvalidOperationException>(() => ReactivityInspection.Use(new RecordingHook()));
            ReactivityInspection.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            registration.Dispose();
            registration.Dispose();
        }
        ReactivityInspection.IsEnabled.ShouldBeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Dependency, WeakReference) CreateObservedOwner()
    {
        var owner = new ReactivePerson { Age = 1 };
        using var effect = Reactive.Effect(() => { _ = owner.Age; });
        return (((IReactiveObject)owner).GetDependency("Age")!, new WeakReference(owner));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Action, WeakReference) CreateCustomReferenceTrigger()
    {
        Action retainedTrigger = null!;
        var reference = Reactive.CustomReference<int>((track, trigger) =>
        {
            retainedTrigger = trigger;
            return (() => 0, _ => { });
        });
        return (retainedTrigger, new WeakReference(reference));
    }

    private sealed class RecordingHook : IReactivityInspectionHook
    {
        internal List<string> Events { get; } = new();
        internal Action? OnObservation { get; init; }
        internal object? ExpectedOwner { get; init; }
        internal bool SawExpectedOwner { get; private set; }
        internal Dependency? LastCause { get; private set; }

        public void DependencyTracked(in ReactivityInspectionDependency dependency)
        {
            SawExpectedOwner |= ExpectedOwner is not null && ReferenceEquals(ExpectedOwner, dependency.Owner);
            Record("track:" + (dependency.DebugLabel ?? dependency.MemberKey as string ?? "anonymous"));
        }

        public void DependencyTriggered(in ReactivityInspectionDependency dependency)
        {
            SawExpectedOwner |= ExpectedOwner is not null && ReferenceEquals(ExpectedOwner, dependency.Owner);
            Record("trigger:" + (dependency.DebugLabel ?? dependency.MemberKey as string ?? "anonymous") +
                (dependency.IsWrite ? ":write" : ":propagation"));
        }

        public void EffectScheduled(in ReactivityInspectionEffect effect)
        {
            LastCause = effect.Cause;
            Record("scheduled");
        }

        public void EffectRunStarted(in ReactivityInspectionEffect effect) => Record("started");
        public void EffectRunCompleted(in ReactivityInspectionEffect effect) => Record("completed:" + effect.Succeeded);

        private void Record(string name)
        {
            Events.Add(name);
            OnObservation?.Invoke();
        }
    }
}
