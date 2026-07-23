using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ReactiveScopeContractTests
{
    [Fact]
    public void Scope_RunAndStop_ExposeTheStateBoundaryNeededByConsumers()
    {
        TestReactiveScope scope = new();

        int result = scope.Run(() => 42);
        scope.Stop();

        result.ShouldBe(42);
        scope.IsActive.ShouldBeFalse();
    }

    private sealed class TestReactiveScope : IReactiveScope
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
