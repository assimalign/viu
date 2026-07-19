using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class EffectScopeTests
{
    [Fact]
    public void EffectsCreatedInScopeStopWithTheScope()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var scope = Reactive.EffectScope();
        scope.Run(() =>
        {
            Reactive.Effect(() =>
            {
                runs++;
                _ = count.Value;
            });
        });
        runs.ShouldBe(1);

        count.Value = 2;
        runs.ShouldBe(2);

        scope.Stop();
        scope.IsActive.ShouldBeFalse();

        count.Value = 3;
        runs.ShouldBe(2);
    }

    [Fact]
    public void NestedScopeStopsWithParent()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var parent = Reactive.EffectScope();
        parent.Run(() =>
        {
            var child = Reactive.EffectScope();
            child.Run(() =>
            {
                Reactive.Effect(() =>
                {
                    runs++;
                    _ = count.Value;
                });
            });
        });
        runs.ShouldBe(1);

        parent.Stop();
        count.Value = 2;
        runs.ShouldBe(1);
    }

    [Fact]
    public void DetachedScopeSurvivesParentStopAndStopsIndependently()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        EffectScope? detached = null;
        var parent = Reactive.EffectScope();
        parent.Run(() =>
        {
            detached = Reactive.EffectScope(detached: true);
            detached.Run(() =>
            {
                Reactive.Effect(() =>
                {
                    runs++;
                    _ = count.Value;
                });
            });
        });
        runs.ShouldBe(1);

        parent.Stop();
        detached!.IsActive.ShouldBeTrue();

        count.Value = 2;
        runs.ShouldBe(2);

        detached.Stop();
        count.Value = 3;
        runs.ShouldBe(2);
    }

    [Fact]
    public void OnScopeDisposeCallbacksFireOnceInRegistrationOrder()
    {
        var order = new List<int>();
        var scope = Reactive.EffectScope();
        scope.Run(() =>
        {
            Reactive.OnScopeDispose(() => order.Add(1));
            Reactive.OnScopeDispose(() => order.Add(2));
            Reactive.OnScopeDispose(() => order.Add(3));
        });
        order.ShouldBeEmpty();

        scope.Stop();
        order.ShouldBe(new[] { 1, 2, 3 });

        // Idempotent: a second stop must not re-fire.
        scope.Stop();
        order.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void OnScopeDisposeWithNoActiveScopeIsSafe()
    {
        Reactive.CurrentScope.ShouldBeNull();
        Should.NotThrow(() => Reactive.OnScopeDispose(() => { }, failSilently: true));
        Should.NotThrow(() => Reactive.OnScopeDispose(() => { }));
    }

    [Fact]
    public void RunReturnsTheFunctionValue()
    {
        var scope = Reactive.EffectScope();
        var result = scope.Run(() => 42);
        result.ShouldBe(42);
    }

    [Fact]
    public void UsingBlockStopsTheScope()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        using (var scope = Reactive.EffectScope())
        {
            scope.Run(() =>
            {
                Reactive.Effect(() =>
                {
                    runs++;
                    _ = count.Value;
                });
            });
        }

        count.Value = 2;
        runs.ShouldBe(1);
    }

    [Fact]
    public void CurrentScopeIsCorrectAcrossNestedRunsAndThrows()
    {
        Reactive.CurrentScope.ShouldBeNull();
        var outer = Reactive.EffectScope();
        outer.Run(() =>
        {
            Reactive.CurrentScope.ShouldBeSameAs(outer);
            var inner = Reactive.EffectScope();
            inner.Run(() => Reactive.CurrentScope.ShouldBeSameAs(inner));
            Reactive.CurrentScope.ShouldBeSameAs(outer);

            Should.Throw<InvalidOperationException>(
                () => inner.Run(() => throw new InvalidOperationException("boom")));
            Reactive.CurrentScope.ShouldBeSameAs(outer);
        });
        Reactive.CurrentScope.ShouldBeNull();
    }

    [Fact]
    public void StoppedScopeNoLongerCollects()
    {
        var count = Reactive.Reference(1);
        var runs = 0;
        var scope = Reactive.EffectScope();
        scope.Stop();

        scope.Run(() =>
        {
            // A stopped scope does not become current, so this effect is unowned.
            Reactive.CurrentScope.ShouldBeNull();
            Reactive.Effect(() =>
            {
                runs++;
                _ = count.Value;
            });
        });
        runs.ShouldBe(1);

        scope.Stop();
        count.Value = 2;
        runs.ShouldBe(2); // the effect was not captured, so it is still live
    }

    [Fact]
    public void ComputedIsNotOwnedByTheScopeAndStaysReactiveAfterStop()
    {
        // Upstream Vue 3.5 contract: effectScope() never collects computeds. After scope.stop()
        // a computed keeps serving FRESH values and stays fully reactive; unused computeds detach
        // from their sources automatically via the soft-unsubscribe protocol instead.
        var count = Reactive.Reference(1);
        Computed<int>? doubled = null;
        var scope = Reactive.EffectScope();
        scope.Run(() => doubled = Reactive.Computed(() => count.Value * 2));

        scope.Stop();

        // A source write after the scope stopped still produces a fresh value.
        count.Value = 5;
        doubled!.Value.ShouldBe(10);

        // A NEW effect reading the computed is fully reactive and re-runs on source changes.
        var effectRuns = 0;
        var seen = 0;
        Reactive.Effect(() =>
        {
            effectRuns++;
            seen = doubled!.Value;
        });
        effectRuns.ShouldBe(1);
        seen.ShouldBe(10);

        count.Value = 6;
        effectRuns.ShouldBe(2);
        seen.ShouldBe(12);
    }

    [Fact]
    public void StopCompletesTeardownAndRethrowsTheFirstOnStopException()
    {
        var count = Reactive.Reference(1);
        var effect2Runs = 0;
        var cleanupRan = false;
        ReactiveEffect? effect2 = null;
        var scope = Reactive.EffectScope();
        scope.Run(() =>
        {
            var effect1 = Reactive.Effect(() => _ = count.Value);
            effect1.OnStop = () => throw new InvalidOperationException("boom");
            effect2 = Reactive.Effect(() =>
            {
                effect2Runs++;
                _ = count.Value;
            });
            Reactive.OnScopeDispose(() => cleanupRan = true);
        });
        effect2Runs.ShouldBe(1);

        var ex = Should.Throw<InvalidOperationException>(scope.Stop);
        ex.Message.ShouldBe("boom");

        // The throwing OnStop did not abandon the rest of the teardown.
        scope.IsActive.ShouldBeFalse();
        effect2!.IsActive.ShouldBeFalse();
        cleanupRan.ShouldBeTrue();

        count.Value = 2;
        effect2Runs.ShouldBe(1);
    }

    [Fact]
    public void CleanupStoppingASiblingChildScopeDoesNotCorruptTeardown()
    {
        var count = Reactive.Reference(1);
        var runsB = 0;
        EffectScope? childA = null;
        EffectScope? childB = null;
        var parent = Reactive.EffectScope();
        parent.Run(() =>
        {
            childA = Reactive.EffectScope();
            childB = Reactive.EffectScope();
            childB.Run(() =>
            {
                Reactive.Effect(() =>
                {
                    runsB++;
                    _ = count.Value;
                });
            });

            // While the parent iterates its children, childA's cleanup re-entrantly stops its
            // sibling childB — which must not invalidate the parent's enumeration.
            childA.Run(() => Reactive.OnScopeDispose(() => childB!.Stop()));
        });
        runsB.ShouldBe(1);

        Should.NotThrow(parent.Stop);

        parent.IsActive.ShouldBeFalse();
        childA!.IsActive.ShouldBeFalse();
        childB!.IsActive.ShouldBeFalse();

        count.Value = 2;
        runsB.ShouldBe(1);
    }

    [Fact]
    public void PauseAndResumeCascadeToChildScopesAndEffects()
    {
        var a = Reactive.Reference(1);
        var b = Reactive.Reference(10);
        var outerRuns = 0;
        var innerRuns = 0;
        var parent = Reactive.EffectScope();
        parent.Run(() =>
        {
            Reactive.Effect(() =>
            {
                outerRuns++;
                _ = a.Value;
            });
            var child = Reactive.EffectScope();
            child.Run(() =>
            {
                Reactive.Effect(() =>
                {
                    innerRuns++;
                    _ = b.Value;
                });
            });
        });
        outerRuns.ShouldBe(1);
        innerRuns.ShouldBe(1);

        parent.Pause();
        a.Value = 2;
        b.Value = 20;
        outerRuns.ShouldBe(1);
        innerRuns.ShouldBe(1);

        parent.Resume();
        outerRuns.ShouldBe(2);
        innerRuns.ShouldBe(2);
    }
}
