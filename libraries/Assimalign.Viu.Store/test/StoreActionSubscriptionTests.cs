using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins Pinia's $onAction (https://pinia.vuejs.org/core-concepts/actions.html#Subscribing-to-actions,
// packages/pinia/src/store.ts): a subscriber runs before the action, its after() hook receives the
// resolved return value (including an awaited Task result) and its onError() hook the thrown
// exception, with after/onError running in registration order.
public sealed class StoreActionSubscriptionTests
{
    [Fact]
    public void OnAction_After_ReceivesReturnValue_OfASyncAction()
    {
        var store = new ModelCounterStore();
        object? afterResult = null;
        string? observedName = null;
        store.OnAction(context =>
        {
            observedName = context.Name;
            context.After(result => afterResult = result);
        }, detached: true);

        var returned = store.IncrementBy(5);

        returned.ShouldBe(5);
        observedName.ShouldBe("IncrementBy");
        afterResult.ShouldBe(5);
    }

    [Fact]
    public async Task OnAction_After_ReceivesAwaitedResult_OfAnAsyncAction()
    {
        var store = new ModelCounterStore();
        object? afterResult = null;
        store.OnAction(context => context.After(result => afterResult = result), detached: true);

        var returned = await store.IncrementByAsync(7);

        returned.ShouldBe(7);
        afterResult.ShouldBe(7);             // the resolved Task<int> value, not the Task itself
    }

    [Fact]
    public void OnAction_OnError_ReceivesTheException_WhichStillPropagates()
    {
        var store = new ModelCounterStore();
        Exception? observed = null;
        store.OnAction(context => context.OnError(exception => observed = exception), detached: true);

        Should.Throw<InvalidOperationException>(store.Explode);

        observed.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void OnAction_After_IsNotInvoked_WhenTheActionThrows()
    {
        var store = new ModelCounterStore();
        var afterInvoked = false;
        store.OnAction(context => context.After(_ => afterInvoked = true), detached: true);

        Should.Throw<InvalidOperationException>(store.Explode);

        afterInvoked.ShouldBeFalse();
    }

    [Fact]
    public void OnAction_RunsBeforeTheActionBody_ThenAfterHooksOnCompletion()
    {
        var store = new ModelCounterStore();
        var order = new List<string>();
        store.OnAction(context =>
        {
            order.Add("before:" + context.Name);
            context.After(_ => order.Add("after"));
        }, detached: true);

        store.Increment();

        order.ShouldBe(new[] { "before:Increment", "after" });
    }

    [Fact]
    public void OnAction_MultipleSubscribers_AfterHooksRunInRegistrationOrder()
    {
        var store = new ModelCounterStore();
        var order = new List<int>();
        store.OnAction(context => context.After(_ => order.Add(1)), detached: true);
        store.OnAction(context => context.After(_ => order.Add(2)), detached: true);

        store.Increment();

        order.ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public void OnAction_AfterStop_NoLongerObservesTheAction()
    {
        var store = new ModelCounterStore();
        var invocations = 0;
        var subscription = store.OnAction(_ => invocations++, detached: true);

        store.Increment();
        invocations.ShouldBe(1);

        subscription.Stop();
        store.Increment();
        invocations.ShouldBe(1);             // stopped: the action is no longer observed
    }

    [Fact]
    public void OnAction_NullCallback_Throws()
    {
        var store = new ModelCounterStore();

        Should.Throw<ArgumentNullException>(() => store.OnAction(null!));
    }
}
