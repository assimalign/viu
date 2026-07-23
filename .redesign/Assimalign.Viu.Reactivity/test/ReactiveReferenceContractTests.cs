using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ReactiveReferenceContractTests
{
    [Fact]
    public void Value_TypedAndUntypedContracts_ExposeTheSameReactiveCell()
    {
        TestReactiveReference<int> reference = new(41);
        IReactiveReference untyped = reference;
        IReactiveReference<int> typed = reference;

        typed.Value++;

        typed.Value.ShouldBe(42);
        untyped.Value.ShouldBe(42);
    }

    private sealed class TestReactiveReference<T> : IReactiveReference<T>
    {
        internal TestReactiveReference(T value)
        {
            Value = value;
        }

        public T Value { get; set; }

        object? IReactiveReference.Value => Value;
    }
}
