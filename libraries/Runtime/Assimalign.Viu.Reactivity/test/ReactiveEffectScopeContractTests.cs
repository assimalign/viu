using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ReactiveEffectScopeContractTests
{
    [Fact]
    public void Scope_RunAndStop_ExposeTheStateBoundaryNeededByConsumers()
    {
        TestReactiveEffectScope scope = new();

        int result = scope.Run(() => 42);
        scope.Stop();

        result.ShouldBe(42);
        scope.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Factory_FirstPartyScope_OwnsAndStopsEffects()
    {
        IReactiveEffectScopeFactory factory = new ReactiveEffectScopeFactory();
        using IReactiveEffectScope scope = factory.Create(isDetached: true);
        IReactiveReference<int> count = Reactive.Reference(0);
        int runs = 0;

        scope.Run(() => Reactive.Effect(() =>
        {
            _ = count.Value;
            runs++;
        }));

        count.Value++;
        scope.Stop();
        count.Value++;

        runs.ShouldBe(2);
        scope.IsActive.ShouldBeFalse();
    }

    private sealed class TestReactiveEffectScope : IReactiveEffectScope
    {
        public bool IsActive { get; private set; } = true;

        public void Run(Action action)
        {
            action();
        }

        public TResult Run<TResult>(Func<TResult> function)
        {
            return function();
        }

        public void Stop()
        {
            IsActive = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
