using System;

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
        IReactiveReadOnlyReference<int> typedReadOnly = reference;
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
        typedReadOnly.Value.ShouldBe(7);
        tracked.Dependency.ShouldBeSameAs(reference.Dependency);
        readOnly.IsReadOnly.ShouldBeFalse();
        runs.ShouldBe(2);
    }

    [Fact]
    public void ReadOnlyReference_Covariance_WidensWithoutExposingASetter()
    {
        IReactiveReadOnlyReference<string> text = Reactive.Reference("value");
        IReactiveReadOnlyReference<object> widened = text;

        widened.Value.ShouldBe("value");
        typeof(IReactiveReadOnlyReference<object>)
            .GetProperty(nameof(IReactiveReadOnlyReference<object>.Value))!
            .CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void Peek_InsideEffect_ReturnsFreshValuesWithoutSubscribingTheCaller()
    {
        Reference<int> reference = Reactive.Reference(1);
        int effectRuns = 0;
        int observed = 0;

        Reactive.Effect(() =>
        {
            effectRuns++;
            observed = reference.Peek();
        });

        reference.Value = 2;

        effectRuns.ShouldBe(1);
        observed.ShouldBe(1);
        reference.Peek().ShouldBe(2);
    }

    [Fact]
    public void Peek_ThrowingGetter_RestoresCallerTracking()
    {
        Reference<int> tracked = Reactive.Reference(1);
        Computed<int> failing = Reactive.Computed<int>(
            () => throw new InvalidOperationException("peek failure"));
        int effectRuns = 0;

        Reactive.Effect(() =>
        {
            effectRuns++;
            Should.Throw<InvalidOperationException>(() => failing.Peek());
            _ = tracked.Value;
        });

        tracked.Value = 2;

        effectRuns.ShouldBe(2);
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
        Reactive.IsReadOnly(computed).ShouldBeTrue();
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
