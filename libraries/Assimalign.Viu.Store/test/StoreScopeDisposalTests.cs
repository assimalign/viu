using Assimalign.Viu;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins the effectScope ownership Pinia gets from effectScope()
// (https://vuejs.org/api/reactivity-advanced.html#effectscope, packages/pinia/src/store.ts): a store
// lives in its own scope, disposing the store or its registry stops that scope, and the store scope
// is detached from whatever scope is active at first use. Run counts (not just values) prove the
// teardown, per the repo testing rule for reactive semantics.
public sealed class StoreScopeDisposalTests
{
    [Fact]
    public void DisposingRegistry_StopsSetupWatchers_SoTheyNoLongerFire()
    {
        var registry = new StoreRegistry();
        var store = Stores.DefineStore("counter", () => new CounterStore()).UseStore(registry);

        store.Increment();                   // 0 -> 1
        store.WatcherRuns.ShouldBe(1);       // the scope-owned watcher fired
        store.Doubled.Value.ShouldBe(2);     // the computed getter tracked the change

        registry.Dispose();

        store.Increment();                   // 1 -> 2, but the store's scope is stopped
        store.WatcherRuns.ShouldBe(1);       // frozen: the setup watcher no longer fires
    }

    [Fact]
    public void DisposingOneStore_StopsItsScope_ButLeavesOtherStoresLive()
    {
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        var useOther = Stores.DefineStore("other", () => new CounterStore());
        var counter = useCounter.UseStore(registry);
        var other = useOther.UseStore(registry);

        useCounter.Dispose(registry);        // dispose only the counter store
        registry.Count.ShouldBe(1);          // counter forgotten; other remains

        counter.Increment();
        counter.WatcherRuns.ShouldBe(0);     // counter's watcher is stopped

        other.Increment();
        other.WatcherRuns.ShouldBe(1);       // other store is unaffected
    }

    [Fact]
    public void DisposedStore_IsRebuiltFresh_OnNextUse()
    {
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        var first = useCounter.UseStore(registry);
        first.Increment();

        useCounter.Dispose(registry);
        var second = useCounter.UseStore(registry);

        second.ShouldNotBeSameAs(first);     // a new instance
        second.Count.Value.ShouldBe(0);      // with fresh state
    }

    [Fact]
    public void StoreScope_IsDetachedFromTheScopeActiveAtFirstUse()
    {
        // Resolving a store inside an ambient scope (e.g. a component's) must NOT capture the store
        // into that scope: stopping the ambient scope leaves the store fully reactive. Only the
        // registry (or the store itself) owns the store's lifetime.
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        CounterStore store = null!;

        var ambient = Reactive.EffectScope();
        ambient.Run(() => store = useCounter.UseStore(registry));
        ambient.Stop();

        store.Increment();
        store.WatcherRuns.ShouldBe(1);       // survived the ambient scope stop

        registry.Dispose();
        store.Increment();
        store.WatcherRuns.ShouldBe(1);       // only registry disposal stops it
    }
}
