using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ComputedTests
{
    [Fact]
    public void GetterIsLazyUntilFirstRead()
    {
        var count = Reactive.Reference(1);
        var getterRuns = 0;
        var doubled = Reactive.Computed(() =>
        {
            getterRuns++;
            return count.Value * 2;
        });
        getterRuns.ShouldBe(0);

        doubled.Value.ShouldBe(2);
        getterRuns.ShouldBe(1);
    }

    [Fact]
    public void RepeatReadsWithoutDepChangeUseTheCache()
    {
        var count = Reactive.Reference(1);
        var getterRuns = 0;
        var doubled = Reactive.Computed(() =>
        {
            getterRuns++;
            return count.Value * 2;
        });

        doubled.Value.ShouldBe(2);
        doubled.Value.ShouldBe(2);
        doubled.Value.ShouldBe(2);
        getterRuns.ShouldBe(1);

        count.Value = 3;
        doubled.Value.ShouldBe(6);
        doubled.Value.ShouldBe(6);
        getterRuns.ShouldBe(2);
    }

    [Fact]
    public void DependencyChangeMarksDirtyButDoesNotRecomputeEagerly()
    {
        var count = Reactive.Reference(1);
        var getterRuns = 0;
        var doubled = Reactive.Computed(() =>
        {
            getterRuns++;
            return count.Value * 2;
        });
        doubled.Value.ShouldBe(2);
        getterRuns.ShouldBe(1);

        count.Value = 5;
        count.Value = 6;
        getterRuns.ShouldBe(1); // nothing recomputed yet

        doubled.Value.ShouldBe(12);
        getterRuns.ShouldBe(2); // exactly one recomputation on read
    }

    [Fact]
    public void ChainedComputedsRecomputeMinimally()
    {
        var a = Reactive.Reference(1);
        var bRuns = 0;
        var cRuns = 0;
        var b = Reactive.Computed(() =>
        {
            bRuns++;
            return a.Value * 2;
        });
        var c = Reactive.Computed(() =>
        {
            cRuns++;
            return b.Value + 1;
        });

        c.Value.ShouldBe(3);
        bRuns.ShouldBe(1);
        cRuns.ShouldBe(1);

        // Cached reads run nothing.
        c.Value.ShouldBe(3);
        b.Value.ShouldBe(2);
        bRuns.ShouldBe(1);
        cRuns.ShouldBe(1);

        a.Value = 2;
        c.Value.ShouldBe(5);
        bRuns.ShouldBe(2);
        cRuns.ShouldBe(2);

        c.Value.ShouldBe(5);
        bRuns.ShouldBe(2);
        cRuns.ShouldBe(2);
    }

    [Fact]
    public void EqualValueRecomputationDoesNotNotifyDownstream()
    {
        var a = Reactive.Reference(1);
        var bRuns = 0;
        var effectRuns = 0;
        var positive = Reactive.Computed(() =>
        {
            bRuns++;
            return a.Value > 0;
        });
        Reactive.Effect(() =>
        {
            effectRuns++;
            _ = positive.Value;
        });
        effectRuns.ShouldBe(1);
        bRuns.ShouldBe(1);

        // 1 -> 2: computed recomputes to the same 'true'; the effect must not re-run.
        a.Value = 2;
        bRuns.ShouldBe(2);
        effectRuns.ShouldBe(1);

        // 2 -> -1: value actually changes; the effect re-runs.
        a.Value = -1;
        bRuns.ShouldBe(3);
        effectRuns.ShouldBe(2);
    }

    [Fact]
    public void ChainedEqualValueCutoffStopsMidChain()
    {
        var a = Reactive.Reference(1);
        var bRuns = 0;
        var cRuns = 0;
        var sign = Reactive.Computed(() =>
        {
            bRuns++;
            return Math.Sign(a.Value);
        });
        var c = Reactive.Computed(() =>
        {
            cRuns++;
            return sign.Value * 100;
        });
        c.Value.ShouldBe(100);
        bRuns.ShouldBe(1);
        cRuns.ShouldBe(1);

        // Sign stays +1: b recomputes, c must not.
        a.Value = 7;
        c.Value.ShouldBe(100);
        bRuns.ShouldBe(2);
        cRuns.ShouldBe(1);
    }

    [Fact]
    public void WritableComputedRoutesAssignmentThroughSetter()
    {
        var first = Reactive.Reference("John");
        var last = Reactive.Reference("Doe");
        var full = Reactive.Computed(
            () => first.Value + " " + last.Value,
            value =>
            {
                var parts = value.Split(' ');
                first.Value = parts[0];
                last.Value = parts[1];
            });
        full.IsWritable.ShouldBeTrue();
        full.Value.ShouldBe("John Doe");

        full.Value = "Jane Smith";
        first.Value.ShouldBe("Jane");
        last.Value.ShouldBe("Smith");
        full.Value.ShouldBe("Jane Smith");
    }

    [Fact]
    public void ReadonlyComputedThrowsOnWrite()
    {
        var c = Reactive.Computed(() => 1);
        c.IsWritable.ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => c.Value = 5);
    }

    [Fact]
    public void EffectOverComputedOverRefPropagates()
    {
        var count = Reactive.Reference(1);
        var getterRuns = 0;
        var effectRuns = 0;
        var seen = 0;
        var scaled = Reactive.Computed(() =>
        {
            getterRuns++;
            return count.Value * 10;
        });
        Reactive.Effect(() =>
        {
            effectRuns++;
            seen = scaled.Value;
        });
        effectRuns.ShouldBe(1);
        getterRuns.ShouldBe(1);
        seen.ShouldBe(10);

        count.Value = 2;
        effectRuns.ShouldBe(2);
        getterRuns.ShouldBe(2);
        seen.ShouldBe(20);
    }

    [Fact]
    public void UnrelatedGlobalChangeDoesNotReRunGetter()
    {
        var tracked = Reactive.Reference(1);
        var unrelated = Reactive.Reference(100);
        var getterRuns = 0;
        var doubled = Reactive.Computed(() =>
        {
            getterRuns++;
            return tracked.Value * 2;
        });
        doubled.Value.ShouldBe(2);
        getterRuns.ShouldBe(1);

        // Bumps the global version but none of the computed's deps.
        unrelated.Value = 101;

        doubled.Value.ShouldBe(2);
        getterRuns.ShouldBe(1);
    }

    [Fact]
    public void ThrowingGetterIsNotPoisonedAndRetriesOnTheNextRead()
    {
        var source = Reactive.Reference(1);
        var calls = 0;
        var flaky = Reactive.Computed(() =>
        {
            calls++;
            var value = source.Value; // dependency established before the throw
            if (calls == 1)
            {
                throw new InvalidOperationException("boom");
            }
            return value * 2;
        });

        // First read: the exception reaches the reader.
        Should.Throw<InvalidOperationException>(() => flaky.Value);
        calls.ShouldBe(1);

        // Second read: the getter is re-invoked (no stale-default fast path) and succeeds.
        flaky.Value.ShouldBe(2);
        calls.ShouldBe(2);

        // Bookkeeping is healthy again: further reads use the cache.
        flaky.Value.ShouldBe(2);
        calls.ShouldBe(2);
    }

    [Fact]
    public void TriggerRefOnAComputedForceRerunsItsEffects()
    {
        var count = Reactive.Reference(1);
        var effectRuns = 0;
        var doubled = Reactive.Computed(() => count.Value * 2);
        Reactive.Effect(() =>
        {
            effectRuns++;
            _ = doubled.Value;
        });
        effectRuns.ShouldBe(1);

        // Upstream triggerRef() force-notifies anything with a dep, including computeds.
        Reactive.TriggerReference(doubled);
        effectRuns.ShouldBe(2);
        doubled.Value.ShouldBe(2); // value itself is unchanged
    }

    [Fact]
    public void ComputedReadInsideEffectTracksLikeADep()
    {
        var count = Reactive.Reference(1);
        var isEven = Reactive.Computed(() => count.Value % 2 == 0);
        var observed = new List<bool>();
        Reactive.Effect(() => observed.Add(isEven.Value));

        count.Value = 2;
        count.Value = 4; // stays even: equal-value cutoff, no effect run
        count.Value = 5;

        observed.ShouldBe(new[] { false, true, false });
    }
}
