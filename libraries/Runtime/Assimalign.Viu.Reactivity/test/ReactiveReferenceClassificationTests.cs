using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ReactiveReferenceClassificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsComputed_AllGenericShapes_DoesNotEvaluateOrTrack(bool writable)
    {
        // [DVT-13]: classification stays separate from writable state and value evaluation.
        VerifyClassification(1, writable);
        VerifyClassification("value", writable);
        VerifyClassification<object>(new object(), writable);
        VerifyClassification(new Dictionary<string, object?>(), writable);
    }

    [Fact]
    public void IsComputed_ExistingExternalImplementation_DefaultsFalseWithoutReadingValue()
    {
        IReactiveReference reference = new ExternalReference();
        reference.IsComputed.ShouldBeFalse();
    }

    private static void VerifyClassification<T>(T value, bool writable)
    {
        Reference<T> source = Reactive.Reference(value);
        int getterRuns = 0;
        int setterRuns = 0;
        Action<T>? setter = writable ? _ => setterRuns++ : null;
        Computed<T> computed = Reactive.Computed(() => { getterRuns++; return source.Value; }, setter);
        IReactiveReference reference = computed;
        int effectRuns = 0;
        using ReactiveEffect effect = Reactive.Effect(() =>
        {
            reference.IsComputed.ShouldBeTrue();
            ((IReactiveReference)source).IsComputed.ShouldBeFalse();
            effectRuns++;
        });

        Reactive.TriggerReference(source);

        computed.IsWritable.ShouldBe(writable);
        getterRuns.ShouldBe(0);
        setterRuns.ShouldBe(0);
        effectRuns.ShouldBe(1);
        reference.Value.ShouldBe(value);
        getterRuns.ShouldBe(1);
    }

    private sealed class ExternalReference : IReactiveReference
    {
        public object? Value => throw new InvalidOperationException("Classification must not read the value.");
    }
}
