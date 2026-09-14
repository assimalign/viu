using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Core.Tests;

/// <summary>Pins deterministic host continuation and cleanup behavior [V01.01.03.20].</summary>
public sealed class RendererParityTestHostTests
{
    /// <summary>Pins one host drain across asynchronous and scheduler work [BLT-13], [SCH-1], [SCH-2].</summary>
    [Fact]
    public void RunUntilIdle_LoadContinuationQueuesRenderAndPostedWork_DrainsOnTheTestThread()
    {
        using RendererParityHost host = new();
        TestSynchronizationContext context = SynchronizationContext.Current
            .ShouldBeOfType<TestSynchronizationContext>();
        int testThreadIdentifier = Environment.CurrentManagedThreadId;
        List<string> order = [];
        TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Record(string phase)
        {
            Environment.CurrentManagedThreadId.ShouldBe(testThreadIdentifier);
            SynchronizationContext.Current.ShouldBeSameAs(context);
            order.Add(phase);
        }

        async Task ContinueLoadAsync()
        {
            await completion.Task;
            Record("continuation");
            Scheduler.QueueJob(new SchedulerJob(() =>
            {
                Record("render");
                context.Post(_ => Record("posted"), null);
            }));
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(() => Record("post-flush")));
        }

        Task operation = ContinueLoadAsync();
        completion.SetResult();

        // An asynchronous completion cannot mutate the host before its explicit drain.
        order.ShouldBeEmpty();
        operation.IsCompleted.ShouldBeFalse();
        Scheduler.IsFlushPending.ShouldBeFalse();

        host.RunUntilIdle().ShouldBe(1);

        operation.IsCompletedSuccessfully.ShouldBeTrue();
        order.ShouldBe(["continuation", "render", "post-flush", "posted"]);
        context.PendingContinuationCount.ShouldBe(0);
        Scheduler.IsFlushPending.ShouldBeFalse();
        host.RunUntilIdle().ShouldBe(0);
    }

    /// <summary>Pins context restoration and repeatable teardown [V01.01.03.20].</summary>
    [Fact]
    public void Dispose_RepeatedCall_RestoresThePreviousSynchronizationContext()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        using RendererParityHost host = new();
        SynchronizationContext.Current.ShouldBeOfType<TestSynchronizationContext>();

        host.Dispose();

        SynchronizationContext.Current.ShouldBeSameAs(previous);
        host.Dispose();
        SynchronizationContext.Current.ShouldBeSameAs(previous);
    }

    /// <summary>Pins context restoration when queued cleanup fails [V01.01.03.20].</summary>
    [Fact]
    public void Dispose_QueuedContinuationThrows_RestoresContextAndPropagatesTheFailure()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        using RendererParityHost host = new();
        TestSynchronizationContext context = SynchronizationContext.Current
            .ShouldBeOfType<TestSynchronizationContext>();
        InvalidOperationException failure = new("cleanup failed");
        context.Post(_ => throw failure, null);

        Should.Throw<InvalidOperationException>(host.Dispose).ShouldBeSameAs(failure);

        SynchronizationContext.Current.ShouldBeSameAs(previous);
        Scheduler.IsFlushPending.ShouldBeFalse();
        host.Dispose();
        SynchronizationContext.Current.ShouldBeSameAs(previous);
    }
}
