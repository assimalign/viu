using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins the runtime watch binding ([V01.01.03.12] wiring the [V01.01.02.06] IWatchScheduler seam,
// upstream apiWatch.ts): the pre-flush default runs callbacks ahead of the render job, Post runs
// them after, repeated triggers in one turn collapse to one delivery, and watcher errors route to
// the app-level errorHandler (issue #28) instead of tearing down the flush.
// https://vuejs.org/api/reactivity-core.html#watch
public sealed class ViuWatchTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ViuWatchTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Watch_DefaultsToPreFlush_RunningBeforeTheRenderJob()
    {
        var state = Reactive.Reference(0);
        var order = new List<string>();
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                ViuWatch.Watch(state, (value, _, _) => order.Add($"watch:{value}"));
                return () =>
                {
                    order.Add($"render:{state.Value}");
                    return VirtualNodeFactory.Text(state.Value.ToString());
                };
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);
        order.ShouldBe(["render:0"]);

        state.Value = 1;
        _pump.RunUntilIdle();

        // Upstream flush: 'pre' — the watcher callback runs in the same flush, before the render.
        order.ShouldBe(["render:0", "watch:1", "render:1"]);
    }

    [Fact]
    public void Watch_PostFlush_RunsAfterTheRenderJob()
    {
        var state = Reactive.Reference(0);
        var order = new List<string>();
        var component = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                ViuWatch.Watch(
                    state,
                    (value, _, _) => order.Add($"watch:{value}"),
                    new WatchOptions { Flush = WatchFlushMode.Post });
                return () =>
                {
                    order.Add($"render:{state.Value}");
                    return VirtualNodeFactory.Text(state.Value.ToString());
                };
            },
        };

        _renderer.Render(VirtualNodeFactory.Component(component), _container);

        state.Value = 1;
        _pump.RunUntilIdle();

        // Upstream flush: 'post' — the callback runs with the post-flush callbacks, after render.
        order.ShouldBe(["render:0", "render:1", "watch:1"]);
    }

    [Fact]
    public void Watch_RepeatedTriggersInOneTurn_DeliverOneCallbackWithTheLatestValue()
    {
        var state = Reactive.Reference(0);
        var runs = 0;
        var observed = (Value: -1, OldValue: -1);
        ViuWatch.Watch(state, (value, oldValue, _) =>
        {
            runs++;
            observed = (value, oldValue);
        });

        state.Value = 1;
        state.Value = 2;
        _pump.RunUntilIdle();

        // One WatchJob maps to one queued SchedulerJob: the turn's mutations collapse into a
        // single delivery observing the final value against the pre-turn old value.
        runs.ShouldBe(1);
        observed.ShouldBe((2, 0));
    }

    [Fact]
    public void WatcherCallbackError_RoutesToTheAppErrorHandler_AndTheFlushSurvives()
    {
        var state = Reactive.Reference(0);
        var captured = new List<(string Message, string Info)>();
        var renderRuns = 0;
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                ViuWatch.Watch(state, (_, _, _) => throw new InvalidOperationException("watcher boom"));
                return () =>
                {
                    renderRuns++;
                    return VirtualNodeFactory.Text(state.Value.ToString());
                };
            },
        };
        var application = _renderer.Renderer.CreateApplication(root);
        application.Config.ErrorHandler = (exception, _, info) => captured.Add((exception.Message, info));
        application.Mount(_container);

        state.Value = 1;
        _pump.RunUntilIdle();

        // Issue #28: watcher errors reach Config.ErrorHandler with the upstream info code, and the
        // flush completes — the component still re-rendered after the failing pre-flush callback.
        captured.ShouldBe([("watcher boom", "watcher callback")]);
        renderRuns.ShouldBe(2);
    }

    [Fact]
    public void WatchEffect_RunsImmediately_AndReRunsOnPreFlushTiming()
    {
        var state = Reactive.Reference(1);
        var observed = new List<int>();
        ViuWatch.WatchEffect(() => observed.Add(state.Value));

        observed.ShouldBe([1]); // immediate first run (upstream watchEffect contract)

        state.Value = 2;
        observed.Count.ShouldBe(1); // nothing synchronous — pre-flush timing

        _pump.RunUntilIdle();
        observed.ShouldBe([1, 2]);
    }
}
