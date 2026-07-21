using System;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins Pinia's $subscribe detached/scope semantics
// (https://pinia.vuejs.org/core-concepts/state.html#Subscribing-to-the-state,
// packages/pinia/src/store.ts + subscriptions.ts): a subscription created inside an effect scope is
// removed when that scope stops unless detached, and notification rides the scheduler's batched flush.
// Stores are resolved through a registry so the fan-out state watcher lives in the store's own scope,
// independent of a subscriber's scope.
public sealed class StoreStateSubscriptionTests : IDisposable
{
    private readonly TestSchedulerPump _pump = TestSchedulerPump.Install();
    private readonly StoreRegistry _registry = new();

    public void Dispose()
    {
        _pump.RunUntilIdle();
        _registry.Dispose();
        _pump.Dispose();
    }

    private ModelCounterStore Resolve()
        => Stores.DefineStore("model-counter", () => new ModelCounterStore()).UseStore(_registry);

    [Fact]
    public void Subscribe_NotifiesOnADirectStateChange()
    {
        var store = Resolve();
        var notifications = 0;
        store.Subscribe((_, _) => notifications++, detached: true);

        store.State.Count = 1;
        _pump.RunUntilIdle();

        notifications.ShouldBe(1);
    }

    [Fact]
    public void Subscribe_CreatedInsideAScope_IsRemovedWhenTheScopeStops()
    {
        var store = Resolve();
        var notifications = 0;
        var scope = Reactive.EffectScope();
        scope.Run(() => store.Subscribe((_, _) => notifications++));   // not detached

        scope.Stop();                        // the component-unmount analogue

        store.State.Count = 1;
        _pump.RunUntilIdle();

        notifications.ShouldBe(0);           // auto-removed with its scope
    }

    [Fact]
    public void Subscribe_Detached_SurvivesTheScopeStop()
    {
        var store = Resolve();
        var notifications = 0;
        var scope = Reactive.EffectScope();
        scope.Run(() => store.Subscribe((_, _) => notifications++, detached: true));

        scope.Stop();

        store.State.Count = 1;
        _pump.RunUntilIdle();

        notifications.ShouldBe(1);           // detached: not tied to the scope
    }

    [Fact]
    public void Subscribe_Stop_RemovesTheSubscription()
    {
        var store = Resolve();
        var notifications = 0;
        var subscription = store.Subscribe((_, _) => notifications++, detached: true);

        store.State.Count = 1;
        _pump.RunUntilIdle();
        notifications.ShouldBe(1);

        subscription.Stop();
        store.State.Count = 2;
        _pump.RunUntilIdle();

        notifications.ShouldBe(1);           // stopped: no further notifications
    }

    [Fact]
    public void Subscribe_NullCallback_Throws()
    {
        var store = Resolve();

        Should.Throw<ArgumentNullException>(() => store.Subscribe(null!));
    }
}
