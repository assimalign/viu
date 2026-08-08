using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class TestSchedulerPumpTests
{
    [Fact]
    public void Dispose_NestedPump_RestoresPreviousDispatcherLease()
    {
        Scheduler.Reset();
        int executions = 0;
        using TestSchedulerPump outer = TestSchedulerPump.Install();
        using (TestSchedulerPump inner = TestSchedulerPump.Install())
        {
            Scheduler.QueueJob(new SchedulerJob(() => executions++));
            inner.PendingFlushCount.ShouldBe(1);
            outer.PendingFlushCount.ShouldBe(0);
            inner.RunUntilIdle().ShouldBe(1);
        }

        Scheduler.QueueJob(new SchedulerJob(() => executions++));
        outer.PendingFlushCount.ShouldBe(1);
        outer.RunUntilIdle().ShouldBe(1);

        executions.ShouldBe(2);
        Scheduler.Reset();
    }
}
