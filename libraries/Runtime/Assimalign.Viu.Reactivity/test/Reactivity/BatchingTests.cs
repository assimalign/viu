using System;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class BatchingTests
{
    [Fact]
    public void BatchCoalescesMultipleWritesIntoOneEffectRun()
    {
        var a = Reactive.Reference(1);
        var b = Reactive.Reference(2);
        var runs = 0;
        var sum = 0;
        Reactive.Effect(() =>
        {
            runs++;
            sum = a.Value + b.Value;
        });
        runs.ShouldBe(1);

        using (Reactive.Batch())
        {
            a.Value = 10;
            b.Value = 20;
            runs.ShouldBe(1); // deferred while the batch is open
        }

        runs.ShouldBe(2);
        sum.ShouldBe(30);
    }

    [Fact]
    public void NestedBatchesFlushOnlyAtTheOutermostDisposeAndDoubleDisposeIsIdempotent()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);

        using IDisposable outerBatch = Reactive.Batch();
        using IDisposable innerBatch = Reactive.Batch();
        count.Value = 2;
        innerBatch.Dispose();
        innerBatch.Dispose();
        runs.ShouldBe(1); // inner disposal and double-disposal do not flush the outer batch

        count.Value = 3;
        outerBatch.Dispose();
        outerBatch.Dispose();
        runs.ShouldBe(2); // one coalesced run
    }

    [Fact]
    public void Batch_ExceptionInsideScope_FlushesAndResumesEffects()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var seen = 0;
        Reactive.Effect(() =>
        {
            runs++;
            seen = count.Value;
        });
        runs.ShouldBe(1);

        Should.Throw<InvalidOperationException>(
            () =>
            {
                using (Reactive.Batch())
                {
                    count.Value = 2;
                    runs.ShouldBe(1);
                    throw new InvalidOperationException("batch failure");
                }
            });

        runs.ShouldBe(2);
        seen.ShouldBe(2);

        count.Value = 3;
        runs.ShouldBe(3);
        seen.ShouldBe(3);
    }

    [Fact]
    public void PauseTrackingSuppressesDependencyCollection()
    {
        var tracked = Reactive.Reference(1);
        var untracked = Reactive.Reference(2);
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

