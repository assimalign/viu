using Shouldly;
using Xunit;

namespace Assimalign.Vue.Reactivity.Tests;

public sealed class BatchingTests
{
    [Fact]
    public void BatchCoalescesMultipleWritesIntoOneEffectRun()
    {
        var a = Reactive.Ref(1);
        var b = Reactive.Ref(2);
        var runs = 0;
        var sum = 0;
        Reactive.Effect(() =>
        {
            runs++;
            sum = a.Value + b.Value;
        });
        runs.ShouldBe(1);

        Reactive.StartBatch();
        a.Value = 10;
        b.Value = 20;
        runs.ShouldBe(1); // deferred while the batch is open
        Reactive.EndBatch();

        runs.ShouldBe(2);
        sum.ShouldBe(30);
    }

    [Fact]
    public void NestedBatchesFlushOnlyAtTheOutermostEnd()
    {
        var count = Reactive.Ref(1);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);

        Reactive.StartBatch();
        Reactive.StartBatch();
        count.Value = 2;
        Reactive.EndBatch();
        runs.ShouldBe(1); // inner end does not flush

        count.Value = 3;
        Reactive.EndBatch();
        runs.ShouldBe(2); // one coalesced run
    }

    [Fact]
    public void EndBatchWithoutStartBatchThrowsAndBatchingStillWorksAfterwards()
    {
        Should.Throw<InvalidOperationException>(Reactive.EndBatch);

        // The failed call must not have corrupted the depth: batching still coalesces.
        var count = Reactive.Ref(1);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);

        Reactive.StartBatch();
        count.Value = 2;
        count.Value = 3;
        runs.ShouldBe(1); // deferred while the batch is open
        Reactive.EndBatch();
        runs.ShouldBe(2);
    }

    [Fact]
    public void PauseTrackingSuppressesDependencyCollection()
    {
        var tracked = Reactive.Ref(1);
        var untracked = Reactive.Ref(2);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = tracked.Value;
            Reactive.PauseTracking();
            _ = untracked.Value;
            Reactive.ResetTracking();
        });
        runs.ShouldBe(1);

        untracked.Value = 20;
        runs.ShouldBe(1);

        tracked.Value = 10;
        runs.ShouldBe(2);
    }
}
