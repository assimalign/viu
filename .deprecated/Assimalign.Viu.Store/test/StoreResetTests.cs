using System;
using System.Collections.Generic;

using Assimalign.Viu.Testing;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins Pinia's $reset (https://pinia.vuejs.org/core-concepts/state.html#Resetting-the-state,
// packages/pinia/src/store.ts): reset restores the initial-state factory's values in place and, per
// Pinia's setup-store rule, a store built without a factory does not implement reset and throws.
public sealed class StoreResetTests : IDisposable
{
    private readonly TestSchedulerPump _pump = TestSchedulerPump.Install();

    public void Dispose()
    {
        _pump.RunUntilIdle();
        _pump.Dispose();
    }

    [Fact]
    public void Reset_RestoresState_ToTheInitialFactoryValues()
    {
        var store = new ModelCounterStore();
        store.State.Count = 7;
        store.State.Step = 4;

        store.Reset();

        store.State.Count.ShouldBe(0);
        store.State.Step.ShouldBe(1);        // both members back to the factory's initial values
    }

    [Fact]
    public void Reset_MutatesInPlace_PreservingStateIdentity()
    {
        var store = new ModelCounterStore();
        var stateBefore = store.State;
        store.State.Count = 7;

        store.Reset();

        store.State.ShouldBeSameAs(stateBefore);   // in-place restore, not a replacement (Pinia parity)
    }

    [Fact]
    public void Reset_NotifiesSubscribersOnce()
    {
        var store = new ModelCounterStore();
        var mutations = new List<StoreMutation>();
        store.Subscribe((mutation, _) => mutations.Add(mutation), detached: true);
        store.State.Count = 3;
        _pump.RunUntilIdle();
        mutations.Clear();

        store.Reset();
        _pump.RunUntilIdle();

        mutations.Count.ShouldBe(1);
    }

    [Fact]
    public void Reset_OnAStoreWithoutAStateFactory_ThrowsDocumentedError()
    {
        var store = new NoResetStore();

        Should.Throw<NotSupportedException>(store.Reset);
    }
}
