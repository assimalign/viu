using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Core.Tests;

public sealed class SchedulerTests : IDisposable
{
    private readonly DeterministicFlushPump _pump;

    public SchedulerTests()
    {
        Scheduler.Reset();
        _pump = new DeterministicFlushPump();
    }

    public void Dispose()
    {
        _pump.Dispose();
        Scheduler.Reset();
    }

    [Fact]
    public void QueueJob_RepeatedInstanceInOneTurn_RunsOnceInOneFlush()
    {
        int runs = 0;
        var job = new SchedulerJob(() => runs++);

        Scheduler.QueueJob(job);
        Scheduler.QueueJob(job);
        Scheduler.QueueJob(job);

        runs.ShouldBe(0);
        _pump.PendingCount.ShouldBe(1);
        _pump.RunUntilIdle().ShouldBe(1);
        runs.ShouldBe(1);
    }

    [Fact]
    public void Flush_PreThenParentRenderThenChildRenderThenPost_UsesNormativeOrder()
    {
        var order = new List<string>();
        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(() => order.Add("post")) { Identifier = 2 });
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("child")) { Identifier = 2 });
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("parent")) { Identifier = 1 });
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("pre"))
            {
                Identifier = 1,
                IsPreFlush = true,
            });

        _pump.RunUntilIdle();

        order.ShouldBe(["pre", "parent", "child", "post"]);
    }

    [Fact]
    public void QueueJob_DuringFlush_InsertsOnlyIntoRemainingOrderedSpan()
    {
        var order = new List<int>();
        Scheduler.QueueJob(
            new SchedulerJob(() =>
            {
                order.Add(1);
                Scheduler.QueueJob(
                    new SchedulerJob(() => order.Add(5)) { Identifier = 5 });
                Scheduler.QueueJob(
                    new SchedulerJob(() => order.Add(3)) { Identifier = 3 });
            })
            {
                Identifier = 1,
            });
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add(4)) { Identifier = 4 });

        _pump.RunUntilIdle();

        order.ShouldBe([1, 3, 4, 5]);
    }

    [Fact]
    public void PostFlushCallbacks_EqualOrderKeys_RetainInsertionOrder()
    {
        var order = new List<int>();
        for (int index = 0; index < 8; index++)
        {
            int capturedIndex = index;
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(() => order.Add(capturedIndex)) { Identifier = 4 });
        }

        _pump.RunUntilIdle();

        order.ShouldBe([0, 1, 2, 3, 4, 5, 6, 7]);
    }

    [Fact]
    public void Job_SelfQueuesWithoutAllowRecurse_IsDeduplicated()
    {
        int runs = 0;
        SchedulerJob? job = null;
        job = new SchedulerJob(() =>
        {
            runs++;
            Scheduler.QueueJob(job!);
        });

        Scheduler.QueueJob(job);
        _pump.RunUntilIdle();

        runs.ShouldBe(1);
    }

    [Fact]
    public void Job_SelfQueuesWithAllowRecurse_RunsAgainInSameChain()
    {
        int runs = 0;
        SchedulerJob? job = null;
        job = new SchedulerJob(() =>
        {
            runs++;
            if (runs < 3)
            {
                Scheduler.QueueJob(job!);
            }
        })
        {
            AllowRecurse = true,
        };

        Scheduler.QueueJob(job);
        _pump.RunUntilIdle();

        runs.ShouldBe(3);
    }

    [Fact]
    public void Job_ExceedsRecursionLimit_AbandonsChainAndNamesJob()
    {
        int runs = 0;
        SchedulerJob? job = null;
        job = new SchedulerJob(() =>
        {
            runs++;
            Scheduler.QueueJob(job!);
        })
        {
            AllowRecurse = true,
            Identifier = 7,
            Name = "runaway watcher",
        };
        Scheduler.QueueJob(job);

        Exception? caught = null;
        try
        {
            _pump.RunUntilIdle();
        }
        catch (Exception exception)
        {
            caught = exception;
        }

        InvalidOperationException failure = caught.ShouldBeOfType<InvalidOperationException>();
        failure.Message.ShouldContain("Maximum recursive updates exceeded");
        failure.Message.ShouldContain("runaway watcher");
        failure.Message.ShouldContain("7");
        runs.ShouldBe(100);
        Scheduler.NextTickAsync().IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void Job_DisposedBeforeFlush_IsSkipped()
    {
        int runs = 0;
        var job = new SchedulerJob(() => runs++);
        Scheduler.QueueJob(job);
        job.IsDisposed = true;

        _pump.RunUntilIdle();

        runs.ShouldBe(0);
    }

    [Fact]
    public void Flush_ThrowingJobAbandonsRemainingWorkAndClearsQueuedFlags()
    {
        int trailingRuns = 0;
        var failing = new SchedulerJob(
            () => throw new InvalidOperationException("scheduler failure"))
        {
            Identifier = 1,
        };
        var trailing = new SchedulerJob(() => trailingRuns++) { Identifier = 2 };
        Scheduler.QueueJob(failing);
        Scheduler.QueueJob(trailing);
        Task tick = Scheduler.NextTickAsync();

        Exception? caught = null;
        try
        {
            _pump.RunUntilIdle();
        }
        catch (Exception exception)
        {
            caught = exception;
        }

        caught.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("scheduler failure");
        trailingRuns.ShouldBe(0);
        tick.IsCompleted.ShouldBeTrue();

        Scheduler.QueueJob(trailing);
        _pump.RunUntilIdle();
        trailingRuns.ShouldBe(1);
    }

    [Fact]
    public void FlushPreFlushCallbacks_RunsOnlyPreJobsImmediately()
    {
        var order = new List<string>();
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("render")));
        Scheduler.QueueJob(
            new SchedulerJob(() => order.Add("pre")) { IsPreFlush = true });

        Scheduler.FlushPreFlushCallbacks();

        order.ShouldBe(["pre"]);
        _pump.RunUntilIdle();
        order.ShouldBe(["pre", "render"]);
    }

    [Fact]
    public async Task NextTickAsync_PostFlushQueuesRender_WaitsForFollowUpCycle()
    {
        var order = new List<string>();
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() =>
        {
            order.Add("post");
            Scheduler.QueueJob(new SchedulerJob(() => order.Add("follow-up")));
        }));
        Task tick = Scheduler.NextTickAsync();

        tick.IsCompleted.ShouldBeFalse();
        _pump.RunUntilIdle();
        await tick;

        order.ShouldBe(["post", "follow-up"]);
    }

    [Fact]
    public async Task NextTickAsync_NoQueuedWork_IsAlreadyCompleted()
    {
        Task tick = Scheduler.NextTickAsync();

        tick.IsCompleted.ShouldBeTrue();
        await tick;
    }

    [Fact]
    public void Reset_DropsPendingWorkClearsQueuedFlagAndMakesStaleDispatchHarmless()
    {
        int runs = 0;
        var job = new SchedulerJob(() => runs++);
        Scheduler.QueueJob(job);

        Scheduler.Reset();
        Scheduler.IsFlushPending.ShouldBeFalse();
        Scheduler.NextTickAsync().IsCompleted.ShouldBeTrue();

        Scheduler.QueueJob(job);
        _pump.RunUntilIdle();

        runs.ShouldBe(1);
    }

    [Fact]
    public void UseFlushDispatcher_NestedLease_RestoresPreviousDispatcher()
    {
        int innerDispatches = 0;
        using (Scheduler.UseFlushDispatcher(_ => innerDispatches++))
        {
            Scheduler.QueueJob(new SchedulerJob(static () => { }));
            innerDispatches.ShouldBe(1);
            Scheduler.Reset();
        }

        Scheduler.QueueJob(new SchedulerJob(static () => { }));
        _pump.PendingCount.ShouldBeGreaterThan(0);
        _pump.RunUntilIdle();
    }

    private sealed class DeterministicFlushPump : IDisposable
    {
        private readonly Queue<Action> _pending = [];
        private readonly IDisposable _registration;

        internal DeterministicFlushPump()
        {
            _registration = Scheduler.UseFlushDispatcher(_pending.Enqueue);
        }

        internal int PendingCount => _pending.Count;

        internal int RunUntilIdle()
        {
            int count = 0;
            while (_pending.Count > 0)
            {
                Action flush = _pending.Dequeue();
                flush();
                count++;
            }

            return count;
        }

        public void Dispose() => _registration.Dispose();
    }
}
