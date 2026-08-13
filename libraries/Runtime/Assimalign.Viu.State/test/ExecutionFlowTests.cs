using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.State;

namespace Assimalign.Viu.State.Tests;

public sealed class ExecutionFlowTests
{
    [Fact]
    public void EnterExecutionFlow_NestedRegistries_RestoresParentRegistryAndDisposesIdempotently()
    {
        using IStateStoreRegistry outerRegistry = StateStores.CreateRegistry();
        using IStateStoreRegistry firstRegistry = StateStores.CreateRegistry();
        using IStateStoreRegistry secondRegistry = StateStores.CreateRegistry();
        StateStores.ActiveRegistry = outerRegistry;

        using IDisposable firstFlow = StateStores.EnterExecutionFlow();
        StateStores.ActiveRegistry.ShouldBeNull();
        StateStores.ActiveRegistry = firstRegistry;

        using IDisposable secondFlow = StateStores.EnterExecutionFlow();
        StateStores.ActiveRegistry.ShouldBeNull();
        StateStores.ActiveRegistry = secondRegistry;

        secondFlow.Dispose();
        secondFlow.Dispose();
        StateStores.ActiveRegistry.ShouldBeSameAs(firstRegistry);

        firstFlow.Dispose();
        firstFlow.Dispose();
        StateStores.ActiveRegistry.ShouldBeSameAs(outerRegistry);

        StateStores.ActiveRegistry = null;
    }

    [Fact]
    public void EnterExecutionFlow_IndependentRegistries_ResolvesOneStorePerFlowOwnedRegistry()
    {
        StateStoreDefinition<CounterStore> definition = StateStores.Define(
            "counter",
            static () => new CounterStore());
        using IStateStoreRegistry outerRegistry = StateStores.CreateRegistry();
        using IStateStoreRegistry isolatedRegistry = StateStores.CreateRegistry();
        StateStores.ActiveRegistry = outerRegistry;
        CounterStore outerStore = definition.Use();

        using (StateStores.EnterExecutionFlow())
        {
            StateStores.ActiveRegistry.ShouldBeNull();
            StateStores.ActiveRegistry = isolatedRegistry;

            CounterStore isolatedStore = definition.Use();

            isolatedStore.ShouldNotBeSameAs(outerStore);
            definition.Use().ShouldBeSameAs(isolatedStore);
            isolatedRegistry.Count.ShouldBe(1);
        }

        StateStores.ActiveRegistry.ShouldBeSameAs(outerRegistry);
        definition.Use().ShouldBeSameAs(outerStore);
        outerRegistry.Count.ShouldBe(1);
        StateStores.ActiveRegistry = null;
    }

    private sealed class CounterStore
    {
    }
}
