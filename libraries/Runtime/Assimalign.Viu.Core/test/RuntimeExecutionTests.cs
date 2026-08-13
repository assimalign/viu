using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu.Core.Tests;

public sealed class RuntimeExecutionTests
{
    [Fact]
    public void EnterExecutionFlow_NestedFlows_SelectFreshAmbientStateAndRestorePredecessors()
    {
        CoreExecutionState sharedCoreState = CoreExecutionIsolation.Current;
        IStateStoreRegistry? previousRegistry = StateStores.ActiveRegistry;
        using IStateStoreRegistry outerRegistry = StateStores.CreateRegistry();
        EffectScope outerScope = Reactive.EffectScope(detached: true);

        try
        {
            outerScope.Run(() =>
            {
                StateStores.ActiveRegistry = outerRegistry;

                IDisposable outerLease = RuntimeExecution.EnterExecutionFlow();
                CoreExecutionState outerCoreState = CoreExecutionIsolation.Current;
                outerCoreState.ShouldNotBeSameAs(sharedCoreState);
                Reactive.CurrentScope.ShouldBeNull();
                StateStores.ActiveRegistry.ShouldBeNull();

                IDisposable innerLease = RuntimeExecution.EnterExecutionFlow();
                CoreExecutionState innerCoreState = CoreExecutionIsolation.Current;
                innerCoreState.ShouldNotBeSameAs(outerCoreState);
                Reactive.CurrentScope.ShouldBeNull();
                StateStores.ActiveRegistry.ShouldBeNull();

                innerLease.Dispose();
                innerLease.Dispose();
                CoreExecutionIsolation.Current.ShouldBeSameAs(outerCoreState);
                Reactive.CurrentScope.ShouldBeNull();
                StateStores.ActiveRegistry.ShouldBeNull();

                outerLease.Dispose();
                outerLease.Dispose();
                CoreExecutionIsolation.Current.ShouldBeSameAs(sharedCoreState);
                Reactive.CurrentScope.ShouldBeSameAs(outerScope);
                StateStores.ActiveRegistry.ShouldBeSameAs(outerRegistry);
            });
        }
        finally
        {
            StateStores.ActiveRegistry = previousRegistry;
            outerScope.Stop();
        }
    }
}
