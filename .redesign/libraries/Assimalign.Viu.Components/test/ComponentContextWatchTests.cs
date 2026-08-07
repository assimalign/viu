using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentContextWatchTests
{
    [Fact]
    public void Watch_ReactiveGetter_UsesContextScopeAndDeliversChanges()
    {
        using StubComponentContext context = new();
        Reference<int> value = Reactive.Reference(1);
        List<(int Value, int PreviousValue)> changes = [];
        using WatchHandle handle = context.Watch(
            () => value.Value,
            (currentValue, previousValue) => changes.Add((currentValue, previousValue)));

        value.Value = 2;

        changes.ShouldBe([(2, 1)]);
        handle.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Watch_CallbackThrows_RoutesFailureThroughContextHook()
    {
        using StubComponentContext context = new();
        Reference<int> value = Reactive.Reference(1);
        using WatchHandle handle = context.Watch<int>(
            () => value.Value,
            (_, _) => throw new InvalidOperationException("watch failure"));

        value.Value = 2;

        context.Errors.ShouldHaveSingleItem().Message.ShouldBe("watch failure");
    }

    [Fact]
    public void Watch_GetterThrows_RoutesFailureAndKeepsWatchAlive()
    {
        using StubComponentContext context = new();
        Reference<int> value = Reactive.Reference(1);
        using WatchHandle handle = context.Watch<int>(
            () => value.Value == 1
                ? throw new InvalidOperationException("getter failure")
                : value.Value,
            (_, _) => { });

        value.Value = 2;

        context.Errors.ShouldHaveSingleItem().Message.ShouldBe("getter failure");
        handle.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Watch_RuntimeScheduler_QueuesPreFlushDelivery()
    {
        TestWatchScheduler scheduler = new();
        using StubComponentContext context = new(scheduler);
        Reference<int> value = Reactive.Reference(1);
        List<(int Value, int PreviousValue)> changes = [];
        using WatchHandle handle = context.Watch(
            () => value.Value,
            (currentValue, previousValue) => changes.Add((currentValue, previousValue)));

        // Core's scheduler adapter selects pre-flush delivery rather than standalone sync [RCT-12].
        value.Value = 2;

        changes.ShouldBeEmpty();
        scheduler.Jobs.ShouldHaveSingleItem().Invoke();
        changes.ShouldBe([(2, 1)]);
    }

    private sealed class StubComponentContext : ComponentContext, IDisposable
    {
        private readonly EffectScope _scope = new();

        internal StubComponentContext(IReactiveWatchScheduler? watchScheduler = null)
        {
            WatchScheduler = watchScheduler;
        }

        public List<Exception> Errors { get; } = [];

        public override ComponentBindings Bindings { get; } = new();

        public override IServiceProvider? Services => null;

        public override ComponentLifecycle Lifecycle { get; } = new();

        public override IReactiveEffectScope Scope => _scope;

        public override IReactiveWatchScheduler? WatchScheduler { get; }

        public override ComponentContext? Parent => null;

        public override void Emit(string name, params object?[] arguments)
        {
        }

        public override void Expose(object? value)
        {
        }

        public override void Warn(string message)
        {
        }

        public void Dispose()
        {
            Lifecycle.Dispose();
            _scope.Dispose();
        }

        protected override void OnWatchError(Exception exception) => Errors.Add(exception);
    }

    private sealed class TestWatchScheduler : IReactiveWatchScheduler
    {
        internal List<WatchJob> Jobs { get; } = [];

        public void Schedule(WatchJob job) => Jobs.Add(job);
    }
}
