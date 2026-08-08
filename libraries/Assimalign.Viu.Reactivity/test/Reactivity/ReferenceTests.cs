using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ReferenceTests
{
    [Fact]
    public void ReferenceHoldsInitialValue()
    {
        var count = Reactive.Reference(41);
        count.Value.ShouldBe(41);
        count.Value = 42;
        count.Value.ShouldBe(42);
    }

    [Fact]
    public void EffectTracksReferenceReadAndRerunsOnWrite()
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
        // EqualityComparer<double>.Default treats NaN as self-equal and also treats +0.0 and -0.0
        // as equal, preserving the collection equality semantics used throughout Viu.
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
    public void ShallowReferenceTriggersOnlyWhenValueIsReplaced()
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
    public void TriggerReferenceForcesNotificationAfterInPlaceMutation()
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
    public void TriggerReferenceWorksOnPlainReferenceToo()
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
    public void CustomReferenceControlsItsOwnTrackAndTrigger()
    {
        // The canonical debounced-reference shape, with a manual flush instead of a timer:
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
            }
        ));

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
    public void NonGenericReactiveValueExposesBoxedValue()
    {
        ReactiveValue boxedReference = Reactive.Reference(3);
        boxedReference.BoxedValue.ShouldBe(3);

        ReactiveValue boxedComputed = Reactive.Computed(() => 9);
        boxedComputed.BoxedValue.ShouldBe(9);
    }
}



