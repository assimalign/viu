using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreActionTests
{
    [Fact]
    public void OnAction_RunsBeforeBodyAndAfterReceivesResult()
    {
        ModelCounterStateStore stateStore = new();
        List<string> order = new();
        object? result = null;
        object? observedStateStore = null;
        stateStore.OnAction(
            context =>
            {
                observedStateStore = context.StateStore;
                order.Add("before:" + context.Name);
                context.After(
                    value =>
                    {
                        result = value;
                        order.Add("after");
                    });
            },
            detached: true);

        int returned = stateStore.IncrementBy(5);

        returned.ShouldBe(5);
        result.ShouldBe(5);
        observedStateStore.ShouldBeSameAs(stateStore);
        order.ShouldBe(
            new[]
            {
                "before:IncrementBy",
                "after",
            });
    }

    [Fact]
    public async Task OnAction_AsyncAfterReceivesAwaitedResult()
    {
        ModelCounterStateStore stateStore = new();
        object? result = null;
        stateStore.OnAction(
            context => context.After(value => result = value),
            detached: true);

        int returned = await stateStore.IncrementByAsync(7);

        returned.ShouldBe(7);
        result.ShouldBe(7);
    }

    [Fact]
    public async Task OnAction_AsyncVoidActionRunsAfterWithNullResult()
    {
        ModelCounterStateStore stateStore = new();
        object? result = new object();
        stateStore.OnAction(
            context => context.After(value => result = value),
            detached: true);

        await stateStore.IncrementAsync();

        result.ShouldBeNull();
        stateStore.State.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OnAction_AsyncFaultRunsErrorHookAndPropagates()
    {
        ModelCounterStateStore stateStore = new();
        Exception? observed = null;
        stateStore.OnAction(
            context => context.OnError(
                exception => observed = exception),
            detached: true);

        await Should.ThrowAsync<InvalidOperationException>(
            stateStore.ExplodeAsync);

        observed.ShouldNotBeNull();
        observed.Message.ShouldBe("async boom");
    }

    [Fact]
    public void OnAction_ErrorHooksRunInOrderAndExceptionPropagates()
    {
        ModelCounterStateStore stateStore = new();
        List<int> order = new();
        bool afterRan = false;
        stateStore.OnAction(
            context =>
            {
                context.After(_ => afterRan = true);
                context.OnError(_ => order.Add(1));
            },
            detached: true);
        stateStore.OnAction(
            context => context.OnError(_ => order.Add(2)),
            detached: true);

        Should.Throw<InvalidOperationException>(stateStore.Explode);

        afterRan.ShouldBeFalse();
        order.ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public void OnAction_AutomaticallyStopsWithCallerScopeUnlessDetached()
    {
        ModelCounterStateStore stateStore = new();
        int scopedRuns = 0;
        int detachedRuns = 0;
        using EffectScope componentScope = Reactive.EffectScope();
        componentScope.Run(
            () =>
            {
                stateStore.OnAction(_ => scopedRuns++);
                stateStore.OnAction(
                    _ => detachedRuns++,
                    detached: true);
            });

        componentScope.Stop();
        stateStore.Increment();

        scopedRuns.ShouldBe(0);
        detachedRuns.ShouldBe(1);
    }

    [Fact]
    public void OnAction_StopRemovesSubscription()
    {
        ModelCounterStateStore stateStore = new();
        int runs = 0;
        StateStoreSubscription subscription =
            stateStore.OnAction(
                _ => runs++,
                detached: true);

        stateStore.Increment();
        subscription.Stop();
        stateStore.Increment();

        runs.ShouldBe(1);
    }

    [Fact]
    public void OnAction_NullCallbackAndHookCallbacksThrow()
    {
        ModelCounterStateStore stateStore = new();
        StateStoreActionContext? context = null;
        stateStore.OnAction(
            value => context = value,
            detached: true);
        stateStore.Increment();

        Should.Throw<ArgumentNullException>(
            () => stateStore.OnAction(null!));
        Should.Throw<ArgumentNullException>(
            () => context!.After(null!));
        Should.Throw<ArgumentNullException>(
            () => context!.OnError(null!));
    }
}
