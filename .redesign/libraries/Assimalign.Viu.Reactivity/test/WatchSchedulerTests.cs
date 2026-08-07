using Assimalign.Viu.Reactivity;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class WatchSchedulerTests
{
    [Fact]
    public void Schedule_ImmediatePolicy_RunsStableJobOnce()
    {
        var runCount = 0;
        var job = new WatchJob(1, () => runCount++);

        new ImmediateWatchScheduler().Schedule(job);

        runCount.ShouldBe(1);
    }
}
