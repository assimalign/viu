using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class RefTests
{
    [Fact]
    public void RefHoldsInitialValue()
    {
        var count = Reactive.Reference(41);
        count.Value.ShouldBe(41);
        count.Value = 42;
        count.Value.ShouldBe(42);
    }

    [Fact]
    public void EffectTracksRefReadAndRerunsOnWrite()
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
        seen.ShouldBe(1);

        count.Value = 7;
        runs.ShouldBe(2);
        seen.ShouldBe(7);
    }

    [Fact]
    public void SettingEqualValueDoesNotTrigger()
    {
        var count = Reactive.Reference(5);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);

        count.Value = 5;
        runs.ShouldBe(1);
    }

    [Fact]
    public void SettingNaNOverNaNDoesNotTrigger()
    {
        // EqualityComparer<double>.Default treats NaN as self-equal (like Object.is; note that
        // unlike Object.is, +0.0 and -0.0 also compare equal — a deliberate .NET divergence).
        var number = Reactive.Reference(double.NaN);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = number.Value;
        });
        runs.ShouldBe(1);

        number.Value = double.NaN;
        runs.ShouldBe(1);
    }

    [Fact]
    public void ShallowRefTriggersOnlyWhenValueIsReplaced()
    {
        var list = Reactive.ShallowReference(new List<int> { 1 });
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = list.Value;
        });
        runs.ShouldBe(1);

        // In-place mutation does not notify.
        list.Value.Add(2);
        runs.ShouldBe(1);

        // Replacement does.
        list.Value = new List<int> { 3 };
        runs.ShouldBe(2);
    }

    [Fact]
    public void TriggerRefForcesNotificationAfterInPlaceMutation()
    {
        var list = Reactive.ShallowReference(new List<int> { 1 });
        var runs = 0;
        var lastCount = 0;
        Reactive.Effect(() =>
        {
            runs++;
            lastCount = list.Value.Count;
        });
        runs.ShouldBe(1);

        list.Value.Add(2);
        runs.ShouldBe(1);

        Reactive.TriggerReference(list);
        runs.ShouldBe(2);
        lastCount.ShouldBe(2);
    }

    [Fact]
    public void TriggerRefWorksOnPlainRefToo()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);

        Reactive.TriggerReference(count);
        runs.ShouldBe(2);
    }

    [Fact]
    public void CustomRefControlsItsOwnTrackAndTrigger()
    {
        // Debounced-ref shape from the Vue docs, with a manual flush instead of a timer:
        // the setter stores the value but defers trigger until Flush() runs.
        Action? flush = null;
        var backing = 0;
        var debounced = Reactive.CustomReference<int>((track, trigger) => (
            Get: () =>
            {
                track();
                return backing;
            },
            Set: value =>
            {
                backing = value;
                flush = trigger;
            }));

        var runs = 0;
        var seen = -1;
        Reactive.Effect(() =>
        {
            runs++;
            seen = debounced.Value;
        });
        runs.ShouldBe(1);
        seen.ShouldBe(0);

        debounced.Value = 5;
        // Deferred: no trigger yet.
        runs.ShouldBe(1);
        seen.ShouldBe(0);

        flush.ShouldNotBeNull();
        flush!();
        runs.ShouldBe(2);
        seen.ShouldBe(5);
    }

    [Fact]
    public void NonGenericIRefExposesBoxedValue()
    {
        IReference boxedRef = Reactive.Reference(3);
        boxedRef.Value.ShouldBe(3);

        IReference boxedComputed = Reactive.Computed(() => 9);
        boxedComputed.Value.ShouldBe(9);
    }
}
