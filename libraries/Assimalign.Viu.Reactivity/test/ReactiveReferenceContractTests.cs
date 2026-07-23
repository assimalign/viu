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

    [Fact]
    public void FirstPartyReference_AllApprovedContracts_ExposeOneTrackedCell()
    {
        Reference<int> reference = Reactive.Reference(7);
        IReactiveReference<int> typed = reference;
        IReactiveReference untyped = reference;
        IReactiveTrackedReference tracked = reference;
        IReactiveReadOnly readOnly = reference;
        int runs = 0;

        Reactive.Effect(() =>
        {
            _ = typed.Value;
            runs++;
        });

        Reactive.TriggerReference(tracked);

        untyped.Value.ShouldBe(7);
        tracked.Dependency.ShouldBeSameAs(reference.Dependency);
        readOnly.IsReadOnly.ShouldBeFalse();
        runs.ShouldBe(2);
    }

    [Fact]
    public void Facade_InterfaceReference_IsRecognizedAndUnwrapped()
    {
        IReactiveReference<int> reference = new TestReactiveReference<int>(41);

        Reactive.IsRef(reference).ShouldBeTrue();
        Reactive.Unref(reference).ShouldBe(41);
    }

    [Fact]
    public void Computed_ReadOnlyContract_ReportsGetterOnlyPolicy()
    {
        IReactiveReference<int> computed = Reactive.Computed(() => 42);

        ((IReactiveReadOnly)computed).IsReadOnly.ShouldBeTrue();
        Reactive.IsReadonly(computed).ShouldBeTrue();
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
