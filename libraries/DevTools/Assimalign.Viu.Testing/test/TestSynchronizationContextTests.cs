using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

// Pins the deterministic async test seam specified by [V01.01.11.05].
public sealed class TestSynchronizationContextTests
{
    [Fact]
    public void Drain_NestedPosts_RemainsNonReentrantAndFirstInFirstOut()
    {
        using TestSynchronizationContext context = TestSynchronizationContext.Install();
        List<string> order = [];
        context.Post(
            _ =>
            {
                order.Add("first-start");
                context.Post(_ => order.Add("nested"), null);
                order.Add("first-end");
            },
            null);
        context.Post(_ => order.Add("second"), null);

        int executed = context.Drain();

        executed.ShouldBe(3);
        order.ShouldBe(["first-start", "first-end", "second", "nested"]);
    }

    [Fact]
    public void Drain_ThrowingContinuation_PropagatesTheOriginalException()
    {
        using TestSynchronizationContext context = TestSynchronizationContext.Install();
        InvalidOperationException expected = new("continuation failed");
        context.Post(_ => throw expected, null);

        InvalidOperationException actual = Should.Throw<InvalidOperationException>(
            () => context.Drain());

        actual.ShouldBeSameAs(expected);
        context.PendingContinuationCount.ShouldBe(0);
    }

    [Fact]
    public void Run_AwaitedContinuation_ResumesDeterministicallyOnTheOwningThread()
    {
        using TestSynchronizationContext context = TestSynchronizationContext.Install();
        int owningThread = Environment.CurrentManagedThreadId;
        List<int> continuationThreads = [];

        context.Run(
            async () =>
            {
                continuationThreads.Add(Environment.CurrentManagedThreadId);
                await Task.Yield();
                continuationThreads.Add(Environment.CurrentManagedThreadId);
            });

        continuationThreads.ShouldBe([owningThread, owningThread]);
    }

    [Fact]
    public void Pump_IncompleteOperationWithoutContinuation_FailsActionably()
    {
        using TestSynchronizationContext context = TestSynchronizationContext.Install();
        TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => context.Pump(completion.Task));

        exception.Message.ShouldContain("has no queued continuation");
        exception.Message.ShouldContain("does not wait on wall-clock or thread-pool timing");
    }

    [Fact]
    public void Dispose_QueuedContinuation_RestoresPreviousContextAndReportsForgottenWork()
    {
        System.Threading.SynchronizationContext? previous =
            System.Threading.SynchronizationContext.Current;
        TestSynchronizationContext context = TestSynchronizationContext.Install();
        context.Post(static _ => { }, null);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(context.Dispose);

        System.Threading.SynchronizationContext.Current.ShouldBeSameAs(previous);
        exception.Message.ShouldContain("1 queued continuation");
        exception.Message.ShouldContain("Call Drain, Pump, or Run");
    }

    [Fact]
    public void SchedulerReset_CapturedFlushBecomesAnOrderedNoOp()
    {
        Scheduler.Reset();
        using TestSynchronizationContext context = TestSynchronizationContext.Install();
        using TestSchedulerPump pump = TestSchedulerPump.Install(context);
        int executions = 0;
        Scheduler.QueueJob(new SchedulerJob(() => executions++));
        pump.PendingFlushCount.ShouldBe(1);

        Scheduler.Reset();
        int dispatched = pump.RunUntilIdle();

        dispatched.ShouldBe(1);
        executions.ShouldBe(0);
        pump.PendingFlushCount.ShouldBe(0);
        Scheduler.Reset();
    }

    [Fact]
    public void TestRenderer_RenderOwnedContext_ExposesExplicitDrain()
    {
        using TestRenderer renderer = new();
        int executions = 0;
        renderer.SynchronizationContext.Post(_ => executions++, null);

        int drained = renderer.Drain();

        drained.ShouldBe(1);
        executions.ShouldBe(1);
    }
}
