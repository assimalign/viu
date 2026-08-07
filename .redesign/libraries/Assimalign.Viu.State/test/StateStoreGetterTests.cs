using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreGetterTests
{
    [Fact]
    public void ComputedGetter_IsLazyAndCachesUntilTrackedMemberChanges()
    {
        ModelCounterStateStore stateStore = new();

        stateStore.DoubledRuns.ShouldBe(0);
        stateStore.Doubled.Value.ShouldBe(0);
        stateStore.Doubled.Value.ShouldBe(0);
        stateStore.DoubledRuns.ShouldBe(1);

        stateStore.State.Step = 10;
        stateStore.Doubled.Value.ShouldBe(0);
        stateStore.DoubledRuns.ShouldBe(1);

        stateStore.State.Count = 4;
        stateStore.Doubled.Value.ShouldBe(8);
        stateStore.DoubledRuns.ShouldBe(2);

        stateStore.State.Count = 4;
        stateStore.Doubled.Value.ShouldBe(8);
        stateStore.DoubledRuns.ShouldBe(2);
    }
}
