using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins the state + getter reactivity integration (Pinia state/getters,
// https://pinia.vuejs.org/core-concepts/state.html + /getters.html): state members are per-member
// trackable and a getter defined as Computed<T> recomputes only when the specific member it read
// changes, matching @vue/reactivity computed() caching. Run counts (not just values) prove the
// caching, per the repo testing rule for reactive semantics.
public sealed class StoreStateGetterTests
{
    [Fact]
    public void Getter_IsLazy_AndCachesUntilItsTrackedStateChanges()
    {
        var store = new ModelCounterStore();

        store.DoubledRuns.ShouldBe(0);       // lazy: the getter body has not run yet

        store.Doubled.Value.ShouldBe(0);     // first read evaluates it
        store.DoubledRuns.ShouldBe(1);

        store.Doubled.Value.ShouldBe(0);     // cached read: no re-evaluation
        store.DoubledRuns.ShouldBe(1);
    }

    [Fact]
    public void Getter_DoesNotRecompute_WhenAnUnreadStateMemberChanges()
    {
        var store = new ModelCounterStore();
        store.Doubled.Value.ShouldBe(0);     // establishes the dependency on Count only
        store.DoubledRuns.ShouldBe(1);

        store.State.Step = 10;               // the getter never read Step

        store.Doubled.Value.ShouldBe(0);     // still cached
        store.DoubledRuns.ShouldBe(1);       // per-member tracking: no recompute
    }

    [Fact]
    public void Getter_Recomputes_ExactlyOnce_WhenItsTrackedStateChanges()
    {
        var store = new ModelCounterStore();
        store.Doubled.Value.ShouldBe(0);
        store.DoubledRuns.ShouldBe(1);

        store.State.Count = 4;               // the tracked member changed

        store.Doubled.Value.ShouldBe(8);
        store.DoubledRuns.ShouldBe(2);       // recomputed exactly once
    }

    [Fact]
    public void Getter_DoesNotRecompute_WhenTrackedStateIsSetToAnEqualValue()
    {
        var store = new ModelCounterStore();
        store.State.Count = 3;
        store.Doubled.Value.ShouldBe(6);
        store.DoubledRuns.ShouldBe(1);

        store.State.Count = 3;               // equal value: the [Reactive] setter does not trigger

        store.Doubled.Value.ShouldBe(6);
        store.DoubledRuns.ShouldBe(1);       // no recompute on an equal-value write
    }
}
