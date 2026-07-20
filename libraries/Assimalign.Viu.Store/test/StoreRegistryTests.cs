using System;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins the per-registry root behavior (Pinia's createPinia() / pinia._s / pinia._e): id uniqueness,
// disposal, and instantiated-store counting.
public sealed class StoreRegistryTests
{
    [Fact]
    public void Resolve_TwoDefinitionsWithSameId_ThrowsDuplicateStoreId()
    {
        var registry = new StoreRegistry();
        var first = Stores.DefineStore("counter", () => new CounterStore());
        var second = Stores.DefineStore("counter", () => new CounterStore());

        first.UseStore(registry);            // claims the id

        var error = Should.Throw<DuplicateStoreIdException>(() => second.UseStore(registry));
        error.StoreId.ShouldBe("counter");
        registry.Count.ShouldBe(1);          // the collision did not register a second store
    }

    [Fact]
    public void SameDefinition_ResolvedTwice_IsNotADuplicate()
    {
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());

        Should.NotThrow(() =>
        {
            useCounter.UseStore(registry);
            useCounter.UseStore(registry);
        });
    }

    [Fact]
    public void Resolve_AfterDispose_ThrowsObjectDisposed()
    {
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        registry.Dispose();

        registry.IsDisposed.ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => useCounter.UseStore(registry));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var registry = new StoreRegistry();
        Stores.DefineStore("counter", () => new CounterStore()).UseStore(registry);

        Should.NotThrow(() =>
        {
            registry.Dispose();
            registry.Dispose();
        });
    }

    [Fact]
    public void Count_ReflectsInstantiatedStores()
    {
        var registry = new StoreRegistry();

        registry.Count.ShouldBe(0);
        Stores.DefineStore("a", () => new CounterStore()).UseStore(registry);
        Stores.DefineStore("b", () => new CounterStore()).UseStore(registry);
        registry.Count.ShouldBe(2);
    }
}
