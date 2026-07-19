using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.RuntimeCore.Tests;

// Pins the flush contract of @vue/runtime-core's scheduler.ts and nextTick —
// https://vuejs.org/api/general.html#nexttick. Tests install the deterministic flush pump so
// the microtask-like flush runs exactly when the test pumps it; the interop-observable WASM
// ordering test is tracked under [V01.01.03.04].
public class SchedulerTests : IDisposable
{
    private readonly TestSchedulerPump _pump;

    public SchedulerTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
    }

    public void Dispose()
    {
        Scheduler.FlushBoundaryCallback = null;
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void QueueJob_DeduplicatesAQueuedJobWithinOneTurn()
    {
        var runs = 0;
        var job = new SchedulerJob(() => runs++);

        Scheduler.QueueJob(job);
        Scheduler.QueueJob(job);
        Scheduler.QueueJob(job);
        runs.ShouldBe(0); // nothing runs synchronously

        _pump.RunUntilIdle();

        runs.ShouldBe(1);
    }

    [Fact]
    public void MultipleJobsQueuedInOneTurn_ProduceExactlyOneFlush()
    {
        var flushedOrder = new List<int>();
        Scheduler.QueueJob(new SchedulerJob(() => flushedOrder.Add(1)) { Identifier = 1 });
        Scheduler.QueueJob(new SchedulerJob(() => flushedOrder.Add(2)) { Identifier = 2 });

        // One posted continuation == one flush for the whole turn.
        _pump.PendingFlushCount.ShouldBe(1);
        _pump.RunUntilIdle();

        flushedOrder.ShouldBe([1, 2]);
    }

    [Fact]
    public void Flush_RunsJobsInIdentifierOrder_ParentBeforeChild_NullLast()
    {
        var order = new List<string>();
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("child")) { Identifier = 5 });
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("anonymous")));
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("parent")) { Identifier = 1 });
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("middle")) { Identifier = 3 });

        _pump.RunUntilIdle();

        // Lower id first (parent components update before children); null id sorts last.
        order.ShouldBe(["parent", "middle", "child", "anonymous"]);
    }

    [Fact]
    public void PreFlushJobsRunBeforeRenderJobs_AndPostFlushCallbacksAfter()
    {
        var order = new List<string>();
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() => order.Add("post")));
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("render")) { Identifier = 1 });
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("pre")) { Identifier = 1, IsPreFlush = true });

        _pump.RunUntilIdle();

        order.ShouldBe(["pre", "render", "post"]);
    }

    // The interop command-buffer seam ([V01.01.04.05]): the boundary callback commits batched DOM
    // mutations after the render queue drains AND again after post-flush callbacks — before them so
    // mounted/updated hooks that read the DOM see the committed render, after them so hooks that
    // write the DOM (e.g. v-show's post-flush updated hook) commit within the same flush. The
    // callback is idempotent (a no-op when nothing is buffered), so a render-only flush still crosses
    // the interop boundary exactly once in practice.
    [Fact]
    public void FlushBoundaryCallback_BracketsPostFlushCallbacks()
    {
        var order = new List<string>();
        Scheduler.FlushBoundaryCallback = () => order.Add("boundary");
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() => order.Add("post")));
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("render")) { Identifier = 1 });

        _pump.RunUntilIdle();

        order.ShouldBe(["render", "boundary", "post", "boundary"]);
    }

    // The buffered batch must commit even when nothing observes it afterwards — otherwise a
    // reactive update with no lifecycle hooks would never reach the DOM.
    [Fact]
    public void FlushBoundaryCallback_FiresEvenWithNoPostFlushCallbacks()
    {
        var boundaryRuns = 0;
        Scheduler.FlushBoundaryCallback = () => boundaryRuns++;
        Scheduler.QueueJob(new SchedulerJob(static () => { }) { Identifier = 1 });

        _pump.RunUntilIdle();

        boundaryRuns.ShouldBe(2); // brackets the (empty) post-flush phase; the second call no-ops
    }

    // The synchronous post-render drain (a direct Render / app mount) also brackets its post-flush
    // callbacks, so a mounted hook both reads the committed tree and commits any DOM it writes.
    [Fact]
    public void FlushBoundaryCallback_BracketsPostFlush_OnSynchronousRenderDrain()
    {
        var order = new List<string>();
        Scheduler.FlushBoundaryCallback = () => order.Add("boundary");
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() => order.Add("post")));

        Scheduler.FlushAfterSynchronousRender();

        order.ShouldBe(["boundary", "post", "boundary"]);
    }

    [Fact]
    public void JobsQueuedDuringAFlush_AreInsertedInIdentifierOrderIntoTheRunningFlush()
    {
        var order = new List<int>();
        Scheduler.QueueJob(new SchedulerJob(() =>
        {
            order.Add(1);
            // Queued mid-flush: id 5 goes after the queued id 4; id 3 runs before it.
            Scheduler.QueueJob(new SchedulerJob(() => order.Add(5)) { Identifier = 5 });
            Scheduler.QueueJob(new SchedulerJob(() => order.Add(3)) { Identifier = 3 });
        })
        { Identifier = 1 });
        Scheduler.QueueJob(new SchedulerJob(() => order.Add(4)) { Identifier = 4 });

        _pump.RunUntilIdle();

        order.ShouldBe([1, 3, 4, 5]);
    }

    [Fact]
    public void AllowRecurse_PermitsControlledSelfRequeueing()
    {
        var runs = 0;
        SchedulerJob? job = null;
        job = new SchedulerJob(() =>
        {
            runs++;
            if (runs < 3)
            {
                Scheduler.QueueJob(job!);
            }
        })
        { AllowRecurse = true };

        Scheduler.QueueJob(job);
        _pump.RunUntilIdle();

        runs.ShouldBe(3);
    }

    [Fact]
    public void WithoutAllowRecurse_SelfRequeueingIsDeduplicatedAway()
    {
        var runs = 0;
        SchedulerJob? job = null;
        job = new SchedulerJob(() =>
        {
            runs++;
            if (runs < 3)
            {
                Scheduler.QueueJob(job!);
            }
        });

        Scheduler.QueueJob(job);
        _pump.RunUntilIdle();

        runs.ShouldBe(1);
    }

    [Fact]
    public void InfiniteUpdateLoop_IsDetectedAtTheRecursionLimit_IdentifyingTheJob()
    {
        var runs = 0;
        SchedulerJob? job = null;
        job = new SchedulerJob(() =>
        {
            runs++;
            Scheduler.QueueJob(job!);
        })
        { AllowRecurse = true, Identifier = 7, Name = "runaway-effect" };

        Scheduler.QueueJob(job);
        // Captured manually: Should.Throw executes delegates off-thread, and the pump must be
        // drained on this thread for the single-threaded scheduler contract to hold.
        Exception? caught = null;
        try
        {
            _pump.RunUntilIdle();
        }
        catch (Exception pumped)
        {
            caught = pumped;
        }

        var exception = caught.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldContain("Maximum recursive updates exceeded");
        exception.Message.ShouldContain("runaway-effect");
        exception.Message.ShouldContain("7");
        runs.ShouldBe(100); // upstream RECURSION_LIMIT
    }

    [Fact]
    public async Task NextTick_CompletesAfterTheFlush_AndObservesPostFlushState()
    {
        var state = "initial";
        Scheduler.QueueJob(new SchedulerJob(() => state = "patched"));

        var tick = Scheduler.NextTick();
        tick.IsCompleted.ShouldBeFalse();

        _pump.RunUntilIdle();

        tick.IsCompleted.ShouldBeTrue();
        await tick;
        state.ShouldBe("patched");
    }

    [Fact]
    public async Task NextTick_WithNothingQueued_IsAlreadyCompleted()
    {
        var tick = Scheduler.NextTick();
        tick.IsCompleted.ShouldBeTrue();
        await tick;
    }

    [Fact]
    public void DisposedJob_IsSkippedByTheFlush()
    {
        var runs = 0;
        var job = new SchedulerJob(() => runs++);
        Scheduler.QueueJob(job);
        job.IsDisposed = true;

        _pump.RunUntilIdle();

        runs.ShouldBe(0);
    }

    [Fact]
    public void WorkQueuedByPostFlushCallbacks_RunsBeforeNextTickResolves()
    {
        var order = new List<string>();
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() =>
        {
            order.Add("post");
            Scheduler.QueueJob(new SchedulerJob(() => order.Add("follow-up-job")));
        }));

        var tick = Scheduler.NextTick();
        _pump.RunUntilIdle();

        order.ShouldBe(["post", "follow-up-job"]);
        tick.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void AThrowingJob_AbandonsTheFlushDeterministically_AndJobsCanRequeue()
    {
        var runs = 0;
        var failing = new SchedulerJob(() => throw new InvalidOperationException("boom")) { Identifier = 1 };
        var trailing = new SchedulerJob(() => runs++) { Identifier = 2 };
        Scheduler.QueueJob(failing);
        Scheduler.QueueJob(trailing);
        var tick = Scheduler.NextTick();

        // Captured manually: Should.Throw executes delegates off-thread, and the pump must be
        // drained on this thread for the single-threaded scheduler contract to hold.
        Exception? caught = null;
        try
        {
            _pump.RunUntilIdle();
        }
        catch (Exception pumped)
        {
            caught = pumped;
        }

        var exception = caught.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldBe("boom");

        // The abandoned job's queued flag was cleared, so it can queue again; NextTick resolved
        // rather than hanging. App-level error routing lands with [V01.01.03.12].
        runs.ShouldBe(0);
        tick.IsCompleted.ShouldBeTrue();
        Scheduler.QueueJob(trailing);
        _pump.RunUntilIdle();
        runs.ShouldBe(1);
    }

    [Fact]
    public void PostFlushCallbacks_WithEqualIds_KeepInsertionOrder()
    {
        // JS array sort is spec-stable; List<T>.Sort is not — the scheduler's explicit
        // insertion-sequence tiebreak is what keeps Mounted hooks child-before-parent.
        var order = new List<string>();
        for (var index = 0; index < 8; index++)
        {
            var name = $"cb{index}";
            Scheduler.QueuePostFlushCallback(new SchedulerJob(() => order.Add(name)));
        }

        _pump.RunUntilIdle();

        order.ShouldBe(["cb0", "cb1", "cb2", "cb3", "cb4", "cb5", "cb6", "cb7"]);
    }

    [Fact]
    public void FlushPreFlushCallbacks_RunsOnlyPreJobsImmediately()
    {
        var order = new List<string>();
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("render")));
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("pre")) { IsPreFlush = true });

        Scheduler.FlushPreFlushCallbacks();

        order.ShouldBe(["pre"]);
        _pump.RunUntilIdle();
        order.ShouldBe(["pre", "render"]);
    }
}
