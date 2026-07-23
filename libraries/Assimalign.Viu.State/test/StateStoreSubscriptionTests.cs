using System;

using Assimalign.Viu.Reactivity;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreSubscriptionTests
{
    [Fact]
    public void Subscribe_CreatedInsideScope_IsRemovedWhenScopeStops()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        int notifications = 0;
        using EffectScope componentScope = Reactive.EffectScope();
        componentScope.Run(
            () => stateStore.Subscribe(
                (_, _) => notifications++));

        componentScope.Stop();
        stateStore.State.Count = 1;
        scheduler.RunUntilIdle();

        notifications.ShouldBe(0);
    }

    [Fact]
    public void Subscribe_Detached_SurvivesCallerScopeStop()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        int notifications = 0;
        using EffectScope componentScope = Reactive.EffectScope();
        componentScope.Run(
            () => stateStore.Subscribe(
                (_, _) => notifications++,
                detached: true));

        componentScope.Stop();
        stateStore.State.Count = 1;
        scheduler.RunUntilIdle();

        notifications.ShouldBe(1);
    }

    [Fact]
    public void Subscribe_StopIsIdempotentAndRemovesCallback()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        int notifications = 0;
        StateStoreSubscription subscription =
            stateStore.Subscribe(
                (_, _) => notifications++,
                detached: true);

        subscription.IsActive.ShouldBeTrue();
        subscription.Stop();
        subscription.Stop();
        subscription.IsActive.ShouldBeFalse();
        stateStore.State.Count = 1;
        scheduler.RunUntilIdle();

        notifications.ShouldBe(0);
    }

    [Fact]
    public void Subscribe_CallbackRemovalDuringNotificationDoesNotCorruptIteration()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        int firstRuns = 0;
        int secondRuns = 0;
        StateStoreSubscription second = null!;
        stateStore.Subscribe(
            (_, _) =>
            {
                firstRuns++;
                second.Stop();
            },
            detached: true);
        second = stateStore.Subscribe(
            (_, _) => secondRuns++,
            detached: true);

        stateStore.State.Count = 1;
        scheduler.RunUntilIdle();

        firstRuns.ShouldBe(1);
        secondRuns.ShouldBe(1);

        stateStore.State.Count = 2;
        scheduler.RunUntilIdle();

        firstRuns.ShouldBe(2);
        secondRuns.ShouldBe(1);
    }

    [Fact]
    public void DisposeRegistry_DeactivatesQueuedStateWatch()
    {
        TestReactiveWatchScheduler scheduler = new();
        StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        ModelCounterStateStore stateStore = Resolve(registry);
        int notifications = 0;
        stateStore.Subscribe(
            (_, _) => notifications++,
            detached: true);
        stateStore.State.Count = 1;
        scheduler.PendingCount.ShouldBe(1);

        registry.Dispose();
        scheduler.RunUntilIdle();

        notifications.ShouldBe(0);
    }

    [Fact]
    public void Subscribe_NullCallback_Throws()
    {
        ModelCounterStateStore stateStore = new();

        Should.Throw<ArgumentNullException>(
            () => stateStore.Subscribe(null!));
    }

    private static ModelCounterStateStore Resolve(
        StateStoreRegistry registry)
    {
        StateStoreDefinition<ModelCounterStateStore> definition =
            StateStores.Define(
                "model-counter",
                static () => new ModelCounterStateStore());
        return definition.Use(registry);
    }
}
