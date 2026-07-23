using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreResetTests
{
    [Fact]
    public void Reset_RestoresFactoryValuesInPlaceAndNotifiesOnce()
    {
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry(scheduler);
        StateStoreDefinition<ModelCounterStateStore> definition =
            StateStores.Define(
                "model-counter",
                static () => new ModelCounterStateStore());
        ModelCounterStateStore stateStore = definition.Use(registry);
        CounterState stateBefore = stateStore.State;
        stateStore.State.Count = 7;
        stateStore.State.Step = 4;
        scheduler.RunUntilIdle();
        List<StateStoreMutation> mutations = new();
        stateStore.Subscribe(
            (mutation, _) => mutations.Add(mutation),
            detached: true);

        stateStore.Reset();
        scheduler.RunUntilIdle();

        stateStore.State.ShouldBeSameAs(stateBefore);
        stateStore.State.Count.ShouldBe(0);
        stateStore.State.Step.ShouldBe(1);
        mutations.Count.ShouldBe(1);
        mutations[0].Kind.ShouldBe(
            StateStorePatchKind.PatchFunction);
    }

    [Fact]
    public void Reset_WithoutFactoryAndApplier_Throws()
    {
        NoResetStateStore stateStore = new();

        Should.Throw<NotSupportedException>(stateStore.Reset);
    }
}
