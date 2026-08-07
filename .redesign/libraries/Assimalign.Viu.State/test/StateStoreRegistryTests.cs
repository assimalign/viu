using Assimalign.Viu.State;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreRegistryTests
{
    [Fact]
    public void Use_SameDefinitionAndRegistry_ReturnsOneRegistryOwnedStore()
    {
        using var registry = StateStores.CreateRegistry();
        var activationCount = 0;
        var definition = new StateStoreDefinition<CounterStore>(
            "counter",
            _ =>
            {
                activationCount++;
                return new CounterStore();
            });

        var first = definition.Use(registry);
        var second = definition.Use(registry);

        second.ShouldBeSameAs(first);
        activationCount.ShouldBe(1);
        registry.Count.ShouldBe(1);
    }

    private sealed class CounterStore
    {
    }
}
