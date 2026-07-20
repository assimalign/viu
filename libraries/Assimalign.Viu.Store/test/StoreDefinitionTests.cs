using System;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins Pinia's defineStore()/useStore() pair (https://pinia.vuejs.org/core-concepts/,
// packages/pinia/src/store.ts): a definition couples an id with a setup, the first resolution per
// registry runs setup once, and later resolutions return the identical instance.
public sealed class StoreDefinitionTests
{
    [Fact]
    public void DefineStore_ReturnsTypedDefinition_CarryingTheId()
    {
        var definition = Stores.DefineStore("counter", () => new CounterStore());

        definition.Id.ShouldBe("counter");
    }

    [Fact]
    public void UseStore_TwiceInOneRegistry_ReturnsSameInstance_AndRunsSetupExactlyOnce()
    {
        var setupRuns = 0;
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () =>
        {
            setupRuns++;
            return new CounterStore();
        });

        var first = useCounter.UseStore(registry);
        var second = useCounter.UseStore(registry);

        first.ShouldBeSameAs(second);        // identical instance (reference equality)
        setupRuns.ShouldBe(1);               // setup ran exactly once per app/registry
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void UseStore_AcrossTwoRegistries_ReturnsIsolatedInstances_WithSeparateSetupRuns()
    {
        // Two registries model two app instances (SSR request isolation): no shared state, setup per
        // app.
        var setupRuns = 0;
        var useCounter = Stores.DefineStore("counter", () =>
        {
            setupRuns++;
            return new CounterStore();
        });
        var registryA = new StoreRegistry();
        var registryB = new StoreRegistry();

        var storeA = useCounter.UseStore(registryA);
        var storeB = useCounter.UseStore(registryB);

        storeA.ShouldNotBeSameAs(storeB);
        setupRuns.ShouldBe(2);               // once per registry

        storeA.Increment();
        storeA.Count.Value.ShouldBe(1);
        storeB.Count.Value.ShouldBe(0);      // fully isolated state
    }

    [Fact]
    public void DefineStore_NullOrEmptyId_Throws()
    {
        Should.Throw<ArgumentException>(() => Stores.DefineStore("", () => new CounterStore()));
        Should.Throw<ArgumentException>(() => Stores.DefineStore(null!, () => new CounterStore()));
    }

    [Fact]
    public void DefineStore_NullSetup_Throws()
        => Should.Throw<ArgumentNullException>(() => Stores.DefineStore<CounterStore>("counter", null!));

    [Fact]
    public void UseStore_NullRegistry_Throws()
    {
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());

        Should.Throw<ArgumentNullException>(() => useCounter.UseStore(null!));
    }
}
