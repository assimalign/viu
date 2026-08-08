using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreScopeDisposalTests
{
    [Fact]
    public void DisposeRegistry_StopsSetupWatchers()
    {
        StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        CounterStore stateStore = definition.Use(registry);
        stateStore.Increment();
        stateStore.WatcherRuns.ShouldBe(1);

        // [STA-2] Registry disposal stops effects created during store setup.
        registry.Dispose();
        stateStore.Increment();

        stateStore.WatcherRuns.ShouldBe(1);
    }

    [Fact]
    public void DisposeDefinition_StopsItsScopeButLeavesOtherStoresLive()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> counterDefinition =
            StateStores.Define("counter", static () => new CounterStore());
        StateStoreDefinition<CounterStore> otherDefinition =
            StateStores.Define("other", static () => new CounterStore());
        CounterStore counter = counterDefinition.Use(registry);
        CounterStore other = otherDefinition.Use(registry);

        counterDefinition.Remove(registry);
        counter.Increment();
        other.Increment();

        counter.WatcherRuns.ShouldBe(0);
        other.WatcherRuns.ShouldBe(1);
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void StoreScope_IsIndependentOfScopeActiveAtFirstUse()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        CounterStore stateStore = null!;
        using EffectScope componentScope = Reactive.EffectScope();

        // [STA-3] A caller's active component scope is never the store scope's parent.
        componentScope.Run(
            () => stateStore = definition.Use(registry));
        componentScope.Stop();
        stateStore.Increment();

        stateStore.WatcherRuns.ShouldBe(1);
        registry.Dispose();
        stateStore.Increment();
        stateStore.WatcherRuns.ShouldBe(1);
    }

    [Fact]
    public void DisposeDefinition_RunsStoreScopeCleanup()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        int cleanupRuns = 0;
        StateStoreDefinition<CounterStore> definition = new(
            "counter",
            _ =>
            {
                Reactive.OnScopeDispose(() => cleanupRuns++);
                return new CounterStore();
            });
        definition.Use(registry);

        definition.Remove(registry);

        cleanupRuns.ShouldBe(1);
    }
}
