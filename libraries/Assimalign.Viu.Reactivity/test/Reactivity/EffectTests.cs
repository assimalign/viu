using System;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class EffectTests
{
    [Fact]
    public void EffectRunsImmediately()
    {
        var runs = 0;
        Reactive.Effect(() => runs++);
        runs.ShouldBe(1);
    }

    [Fact]
    public void ConditionalBranchStopsReactingToAbandonedBranch()
    {
        var flag = Reactive.Reference(true);
        var a = Reactive.Reference(1);
        var b = Reactive.Reference(10);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = flag.Value ? a.Value : b.Value;
        });
        runs.ShouldBe(1);

        a.Value = 2;
        runs.ShouldBe(2);

        // While on the 'a' branch, b is not a dependency.
        b.Value = 11;
        runs.ShouldBe(2);

        flag.Value = false;
        runs.ShouldBe(3);

        // Abandoned branch: a must no longer notify the effect.
        a.Value = 3;
        runs.ShouldBe(3);

        b.Value = 12;
        runs.ShouldBe(4);
    }

    [Fact]
    public void NestedEffectsTrackToTheCorrectOwner()
    {
        var outerRef = Reactive.Reference(1);
        var innerRef = Reactive.Reference(10);
        var outerRuns = 0;
        var innerRuns = 0;
        Reactive.Effect(() =>
        {
            outerRuns++;
            Reactive.Effect(() =>
            {
                innerRuns++;
                _ = innerRef.Value;
            });
            _ = outerRef.Value;
        });
        outerRuns.ShouldBe(1);
        innerRuns.ShouldBe(1);

        // Inner dep must not leak into the outer effect.
        innerRef.Value = 11;
        outerRuns.ShouldBe(1);
        innerRuns.ShouldBe(2);

        // Outer dep read after the inner effect was created still belongs to the outer effect.
        outerRef.Value = 2;
        outerRuns.ShouldBe(2);
        innerRuns.ShouldBe(3); // outer re-run created a second inner effect
    }

    [Fact]
    public void ActiveSubscriberIsRestoredWhenInnerEffectThrows()
    {
        var count = Reactive.Reference(1);
        var outerRuns = 0;
        Reactive.Effect(() =>
        {
            outerRuns++;
            try
            {
                new ReactiveEffect(() => throw new InvalidOperationException("boom")).Run();
            }
            catch (InvalidOperationException)
            {
            }

            // Read AFTER the inner throw: must still track to the outer effect.
            _ = count.Value;
        });
        outerRuns.ShouldBe(1);

        count.Value = 2;
        outerRuns.ShouldBe(2);
    }

    [Fact]
    public void StopDetachesTheEffect()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var effect = Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);
        effect.IsActive.ShouldBeTrue();

        effect.Stop();
        effect.IsActive.ShouldBeFalse();

        count.Value = 2;
        runs.ShouldBe(1);
    }

    [Fact]
    public void RunAfterStopExecutesUntracked()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var effect = Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        effect.Stop();

        effect.Run();
        runs.ShouldBe(2);

        // The manual run did not re-establish tracking.
        count.Value = 2;
        runs.ShouldBe(2);
    }

    [Fact]
    public void EffectThrowingOnFirstRunIsStoppedAndLeavesNoSubscription()
    {
        var count = Reactive.Reference(1);
        var runs = 0;

        // The exception from the first run propagates to the caller (upstream effect() parity)...
        Should.Throw<InvalidOperationException>(() => Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
            throw new InvalidOperationException("boom");
        }));
        runs.ShouldBe(1);

        // ...and the effect was stopped: a later write is a silent no-op (no re-run, no rethrow).
        Should.NotThrow(() => count.Value = 2);
        runs.ShouldBe(1);
    }

    [Fact]
    public void OnStopFiresExactlyOnce()
    {
        var stops = 0;
        var effect = Reactive.Effect(() => { });
        effect.OnStop = () => stops++;

        effect.Stop();
        effect.Stop();
        stops.ShouldBe(1);
    }

    [Fact]
    public void SchedulerReceivesInvalidationInsteadOfAutoRun()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var seen = 0;
        var invalidations = 0;
        var effect = Reactive.Effect(
            () =>
            {
                runs++;
                seen = count.Value;
            },
            scheduler: () => invalidations++);
        runs.ShouldBe(1);
        invalidations.ShouldBe(0);

        count.Value = 5;
        invalidations.ShouldBe(1);
        runs.ShouldBe(1); // not auto-run
        seen.ShouldBe(1);

        // Manual re-run picks up the new state.
        effect.Run();
        runs.ShouldBe(2);
        seen.ShouldBe(5);
    }

    [Fact]
    public void SchedulerIsInvokedOncePerBatch()
    {
        var a = Reactive.Reference(1);
        var b = Reactive.Reference(2);
        var invalidations = 0;
        Reactive.Effect(
            () =>
            {
                _ = a.Value;
                _ = b.Value;
            },
            scheduler: () => invalidations++);

        Reactive.StartBatch();
        a.Value = 10;
        b.Value = 20;
        a.Value = 11;
        Reactive.EndBatch();

        invalidations.ShouldBe(1);
    }

    [Fact]
    public void SelfMutatingEffectDoesNotInfinitelyLoop()
    {
        var count = Reactive.Reference(0);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            count.Value = count.Value + 1;
        });
        runs.ShouldBe(1);
        count.Value.ShouldBe(1);

        // External write re-runs once; the effect's own write is again suppressed.
        count.Value = 10;
        runs.ShouldBe(2);
        count.Value.ShouldBe(11);
    }

    [Fact]
    public void AllowRecurseLetsAnEffectReTriggerItself()
    {
        var count = Reactive.Reference(0);
        var runs = 0;
        var effect = new ReactiveEffect(() =>
        {
            runs++;
            if (count.Value < 3)
            {
                count.Value++;
            }
        })
        {
            AllowRecurse = true,
        };
        effect.Run();

        count.Value.ShouldBe(3);
        runs.ShouldBe(4);
    }

    [Fact]
    public void PauseDefersRunsAndResumeDeliversSingleTrailingRun()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var effect = Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);

        effect.Pause();
        count.Value = 2;
        count.Value = 3;
        runs.ShouldBe(1);

        effect.Resume();
        runs.ShouldBe(2);

        // Resume with nothing pending does not run.
        effect.Pause();
        effect.Resume();
        runs.ShouldBe(2);
    }

    [Fact]
    public void RunIfDirtyOnlyRunsWhenADependencyChanged()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var effect = Reactive.Effect(
            () =>
            {
                runs++;
                _ = count.Value;
            },
            scheduler: () => { });
        runs.ShouldBe(1);

        effect.RunIfDirty();
        runs.ShouldBe(1);

        count.Value = 2;
        effect.RunIfDirty();
        runs.ShouldBe(2);
    }
}

