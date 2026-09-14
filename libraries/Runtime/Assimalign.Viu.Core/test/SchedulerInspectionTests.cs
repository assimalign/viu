using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

/// <summary>Pins the non-interfering scheduler observation contract of [DVT-8] and [DVT-10].</summary>
public sealed class SchedulerInspectionTests : IDisposable
{
    private readonly Queue<Action> _pending = [];
    private readonly IDisposable _dispatcher;

    public SchedulerInspectionTests()
    {
        Scheduler.Reset();
        _dispatcher = Scheduler.UseFlushDispatcher(_pending.Enqueue);
    }

    public void Dispose()
    {
        _dispatcher.Dispose();
        Scheduler.Reset();
    }

    [Fact]
    public void Flush_AllPhasesAndPostGeneratedWork_ReportsOneCorrelatedChainBeforeNextTick()
    {
        var order = new List<string>();
        var hook = new RecordingHook(order);
        using IDisposable registration = RuntimeInspection.Use(hook);
        long reservedIdentifier = RuntimeInspection.FlushIdentifier;
        RuntimeInspection.FlushIdentifier.ShouldBe(reservedIdentifier);
        _pending.Count.ShouldBe(0);
        var render = new SchedulerJob(() => order.Add("render"));
        Scheduler.QueueJob(render);
        Scheduler.QueueJob(render);
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("disposed")) { IsDisposed = true });
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("pre")) { IsPreFlush = true });
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() =>
        {
            order.Add("post");
            Scheduler.QueueJob(new SchedulerJob(() => order.Add("nested-render")));
        }));
        Task completion = Scheduler.NextTickAsync();
        bool completedBeforeObservation = true;
        hook.OnCompleted = () => completedBeforeObservation = completion.IsCompleted;

        RunUntilIdle();

        order.ShouldBe(["started", "pre", "render", "post", "nested-render", "completed"]);
        hook.Started.Count.ShouldBe(1);
        hook.Completed.Count.ShouldBe(1);
        hook.Started[0].Identifier.ShouldBe(reservedIdentifier);
        hook.Started[0].PreFlushCount.ShouldBe(0);
        RuntimeInspectionFlush flush = hook.Completed[0];
        flush.Identifier.ShouldBe(reservedIdentifier);
        flush.PreFlushCount.ShouldBe(1);
        flush.RenderCount.ShouldBe(2);
        flush.PostFlushCount.ShouldBe(1);
        flush.Faulted.ShouldBeFalse();
        completedBeforeObservation.ShouldBeFalse();
        completion.IsCompleted.ShouldBeTrue();
        RuntimeInspection.FlushIdentifier.ShouldBeGreaterThan(reservedIdentifier);
        _pending.Count.ShouldBe(0);
    }

    [Fact]
    public void Flush_ThrowingJob_ReportsFaultAndAttemptedCountsThenRecovers()
    {
        var hook = new RecordingHook([]);
        using IDisposable registration = RuntimeInspection.Use(hook);
        Scheduler.QueueJob(new SchedulerJob(static () => throw new InvalidOperationException("job")));
        Scheduler.QueueJob(new SchedulerJob(static () => { }));
        Task completion = Scheduler.NextTickAsync();

        Should.Throw<InvalidOperationException>(RunUntilIdle).Message.ShouldBe("job");

        hook.Started.Count.ShouldBe(1);
        hook.Completed.Count.ShouldBe(1);
        hook.Completed[0].Identifier.ShouldBe(hook.Started[0].Identifier);
        hook.Completed[0].RenderCount.ShouldBe(1);
        hook.Completed[0].Faulted.ShouldBeTrue();
        completion.IsCompleted.ShouldBeTrue();
        Scheduler.QueueJob(new SchedulerJob(static () => { }));
        RunUntilIdle();
        hook.Completed.Count.ShouldBe(2);
        hook.Completed[1].Faulted.ShouldBeFalse();
        hook.Completed[1].Identifier.ShouldBeGreaterThan(hook.Completed[0].Identifier);
    }

    [Fact]
    public void SynchronousDrain_ConsumesPendingPhases_ReportsOnePairAndIgnoresStaleContinuation()
    {
        var order = new List<string>();
        var hook = new RecordingHook(order);
        using IDisposable registration = RuntimeInspection.Use(hook);
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("pre")) { IsPreFlush = true });
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() => order.Add("post")));

        Scheduler.FlushPreFlushCallbacks();
        Scheduler.FlushAfterSynchronousRender();
        RunUntilIdle();

        order.ShouldBe(["started", "pre", "post", "completed"]);
        hook.Completed.Count.ShouldBe(1);
        hook.Completed[0].PreFlushCount.ShouldBe(1);
        hook.Completed[0].PostFlushCount.ShouldBe(1);
        hook.Completed[0].RenderCount.ShouldBe(0);
    }

    [Fact]
    public void SynchronousDrain_RemainingRenderJobs_KeepIdentityUntilScheduledChainCompletes()
    {
        var hook = new RecordingHook([]);
        using IDisposable registration = RuntimeInspection.Use(hook);
        Scheduler.QueueJob(new SchedulerJob(static () => { }) { IsPreFlush = true });
        Scheduler.QueueJob(new SchedulerJob(static () => { }));

        Scheduler.FlushAfterSynchronousRender();
        hook.Started.Count.ShouldBe(1);
        hook.Completed.Count.ShouldBe(0);
        RunUntilIdle();

        hook.Started.Count.ShouldBe(1);
        hook.Completed.Count.ShouldBe(1);
        hook.Completed[0].Identifier.ShouldBe(hook.Started[0].Identifier);
        hook.Completed[0].PreFlushCount.ShouldBe(1);
        hook.Completed[0].RenderCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SynchronousDrain_ThrowingPhase_ReportsFaultAndCompletesNextTick(bool preFlush)
    {
        var hook = new RecordingHook([]);
        using IDisposable registration = RuntimeInspection.Use(hook);
        var failingJob = new SchedulerJob(static () => throw new InvalidOperationException("phase"))
        {
            IsPreFlush = preFlush,
        };
        if (preFlush)
        {
            Scheduler.QueueJob(failingJob);
        }
        else
        {
            Scheduler.QueuePostFlushCallback(failingJob);
        }

        Task completion = Scheduler.NextTickAsync();

        Should.Throw<InvalidOperationException>(Scheduler.FlushAfterSynchronousRender)
            .Message.ShouldBe("phase");
        RunUntilIdle();

        hook.Started.Count.ShouldBe(1);
        hook.Completed.Count.ShouldBe(1);
        hook.Completed[0].Identifier.ShouldBe(hook.Started[0].Identifier);
        hook.Completed[0].Faulted.ShouldBeTrue();
        hook.Completed[0].PreFlushCount.ShouldBe(preFlush ? 1 : 0);
        hook.Completed[0].PostFlushCount.ShouldBe(preFlush ? 0 : 1);
        completion.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void Hooks_ReadReactiveValueAndThrow_NeverSubscribeAmbientEffectOrAlterJobDelivery()
    {
        Reference<int> observed = Reactive.Reference(0);
        var hook = new RecordingHook([])
        {
            OnStarted = () =>
            {
                _ = observed.Value;
                throw new InvalidOperationException("inspection");
            },
            OnCompleted = () =>
            {
                _ = observed.Value;
                throw new InvalidOperationException("inspection");
            },
        };
        using IDisposable registration = RuntimeInspection.Use(hook);
        int effectRuns = 0;
        int jobRuns = 0;
        using ReactiveEffect effect = Reactive.Effect(() =>
        {
            effectRuns++;
            Scheduler.QueuePostFlushCallback(new SchedulerJob(() => jobRuns++));
            Scheduler.FlushAfterSynchronousRender();
        });

        observed.Value = 1;
        RunUntilIdle();

        effectRuns.ShouldBe(1);
        jobRuns.ShouldBe(1);
        hook.Started.Count.ShouldBe(1);
        hook.Completed.Count.ShouldBe(1);
    }

    [Fact]
    public void NoInstalledHook_ReservesNoIdentifierAndObservesNothing()
    {
        // [DVT-12]: the enabled check is the sole guard; support is read once per process
        // (RuntimeInspectionSupport), so the observable disabled state is "no hook installed".
        RuntimeInspection.IsSupported.ShouldBeTrue();
        RuntimeInspection.IsEnabled.ShouldBeFalse();
        RuntimeInspection.FlushIdentifier.ShouldBe(0);
        Scheduler.QueueJob(new SchedulerJob(static () => { }));

        RunUntilIdle();

        RuntimeInspection.FlushIdentifier.ShouldBe(0);
    }

    private void RunUntilIdle()
    {
        while (_pending.Count > 0)
        {
            _pending.Dequeue()();
        }
    }

    private sealed class RecordingHook(List<string> order) : IRuntimeInspectionHook, IRuntimeSchedulerInspectionHook
    {
        internal List<RuntimeInspectionFlush> Started { get; } = [];

        internal List<RuntimeInspectionFlush> Completed { get; } = [];

        internal Action? OnStarted { get; init; }

        internal Action? OnCompleted { get; set; }

        public void FlushStarted(in RuntimeInspectionFlush flush)
        {
            order.Add("started");
            Started.Add(flush);
            OnStarted?.Invoke();
        }

        public void FlushCompleted(in RuntimeInspectionFlush flush)
        {
            order.Add("completed");
            Completed.Add(flush);
            OnCompleted?.Invoke();
        }

        public void ComponentMounted(in RuntimeInspectionComponent component) { }

        public void ComponentUpdated(in RuntimeInspectionComponent component) { }

        public void ComponentUnmounted(object instance) { }

        public void ComponentReordered(object instance, int index) { }

        public void ComponentEvent(object instance, string name, IReadOnlyList<object?> arguments) { }
    }
}
