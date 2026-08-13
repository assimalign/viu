using System;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class ExecutionFlowTests
{
    [Fact]
    public void EnterExecutionFlow_NestedScopes_RestoresParentScopeAndDisposesIdempotently()
    {
        using EffectScope outerScope = Reactive.EffectScope(detached: true);

        outerScope.Run(
            () =>
            {
                Reactive.CurrentScope.ShouldBeSameAs(outerScope);

                using IDisposable firstFlow = Reactive.EnterExecutionFlow();
                Reactive.CurrentScope.ShouldBeNull();

                using EffectScope firstFlowScope = Reactive.EffectScope();
                firstFlowScope.Run(
                    () =>
                    {
                        Reactive.CurrentScope.ShouldBeSameAs(firstFlowScope);

                        using IDisposable secondFlow = Reactive.EnterExecutionFlow();
                        Reactive.CurrentScope.ShouldBeNull();

                        secondFlow.Dispose();
                        secondFlow.Dispose();
                        Reactive.CurrentScope.ShouldBeSameAs(firstFlowScope);
                    });

                firstFlow.Dispose();
                firstFlow.Dispose();
                Reactive.CurrentScope.ShouldBeSameAs(outerScope);
            });

        Reactive.CurrentScope.ShouldBeNull();
    }

    [Fact]
    public void EnterExecutionFlow_OuterBatch_UsesFreshDeliveryStateAndRestoresOuterBatch()
    {
        Reference<int> outerValue = Reactive.Reference(0);
        Reference<int> isolatedValue = Reactive.Reference(0);
        int outerRuns = 0;
        int isolatedRuns = 0;
        using ReactiveEffect outerEffect = Reactive.Effect(
            () =>
            {
                outerRuns++;
                _ = outerValue.Value;
            });

        using IDisposable outerBatch = Reactive.Batch();
        outerValue.Value = 1;
        outerRuns.ShouldBe(1);

        using IDisposable executionFlow = Reactive.EnterExecutionFlow();
        using ReactiveEffect isolatedEffect = Reactive.Effect(
            () =>
            {
                isolatedRuns++;
                _ = isolatedValue.Value;
            });

        isolatedValue.Value = 1;
        isolatedRuns.ShouldBe(2);

        executionFlow.Dispose();
        executionFlow.Dispose();
        outerValue.Value = 2;
        outerRuns.ShouldBe(1);

        outerBatch.Dispose();
        outerRuns.ShouldBe(2);
    }
}
