using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

public sealed class BaseTransitionTests
{
    [Fact]
    public void Render_InitialMountWithoutAppear_SkipsEnterHooks()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TransitionRecorder recorder = new();
        Reference<bool> visible = Reactive.Reference(true);
        TransitionHarness harness = Mount(
            recorder.Build(),
            () => visible.Value
                ? ComponentTree.Element("div", key: "content")
                : ComponentTree.Comment());

        pump.RunUntilIdle();

        recorder.Calls.ShouldBeEmpty();
        harness.Host.Root.Children.Count.ShouldBe(1);
        harness.Host.Root.Children[0].Content.ShouldBe("div");
    }

    [Fact]
    public void Render_LeaveAndReenter_DefersRemovalAndRunsEachPhaseOnce()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TransitionRecorder recorder = new();
        Reference<bool> visible = Reactive.Reference(true);
        TransitionHarness harness = Mount(
            recorder.Build(),
            () => visible.Value
                ? ComponentTree.Element("div", key: "content")
                : ComponentTree.Comment());
        pump.RunUntilIdle();

        visible.Value = false;
        pump.RunUntilIdle();

        recorder.Calls.ShouldBe(["beforeLeave", "leave"]);
        harness.Host.Root.Children.Count.ShouldBe(2);
        recorder.CompleteLeave();
        harness.Host.Root.Children.Count.ShouldBe(1);
        recorder.Calls.ShouldBe(["beforeLeave", "leave", "afterLeave"]);

        visible.Value = true;
        pump.RunUntilIdle();

        recorder.Calls.ShouldBe(
        [
            "beforeLeave",
            "leave",
            "afterLeave",
            "beforeEnter",
            "enter",
        ]);
        recorder.CompleteEnter();
        recorder.Calls[^1].ShouldBe("afterEnter");
        harness.Host.Root.Children.Count.ShouldBe(1);
        harness.Host.Root.Children[0].Content.ShouldBe("div");
    }

    [Fact]
    public void Render_Appear_UsesAppearHooks()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TransitionRecorder recorder = new();
        TransitionHarness harness = Mount(
            recorder.Build(appear: true),
            static () => ComponentTree.Element("div"));

        pump.RunUntilIdle();

        recorder.Calls.ShouldBe(["beforeAppear", "appear"]);
        recorder.CompleteEnter();
        recorder.Calls.ShouldBe(
        [
            "beforeAppear",
            "appear",
            "afterAppear",
        ]);
        harness.Host.Root.Children.Count.ShouldBe(1);
    }

    [Fact]
    public void Render_OutIn_LeavesOldBeforeMountingNew()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TransitionRecorder recorder = new();
        Reference<string> selected = Reactive.Reference("div");
        TransitionHarness harness = Mount(
            recorder.Build(mode: "out-in"),
            () => selected.Value == "div"
                ? ComponentTree.Element("div", key: "first")
                : ComponentTree.Element("span", key: "second"));
        pump.RunUntilIdle();

        selected.Value = "span";
        pump.RunUntilIdle();

        recorder.Calls.ShouldBe(["beforeLeave", "leave"]);
        harness.Host.Root.Children.ShouldContain(
            node => node.Content == "div");
        harness.Host.Root.Children.ShouldNotContain(
            node => node.Content == "span");

        recorder.CompleteLeave();
        pump.RunUntilIdle();

        harness.Host.Root.Children.ShouldNotContain(
            node => node.Content == "div");
        harness.Host.Root.Children.ShouldContain(
            node => node.Content == "span");
        recorder.Calls.ShouldContain("beforeEnter");
        recorder.Calls.ShouldContain("enter");
    }

    [Fact]
    public void Render_InOut_EntersNewBeforeLeavingOld()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        TransitionRecorder recorder = new();
        Reference<string> selected = Reactive.Reference("div");
        TransitionHarness harness = Mount(
            recorder.Build(mode: "in-out"),
            () => selected.Value == "div"
                ? ComponentTree.Element("div", key: "first")
                : ComponentTree.Element("span", key: "second"));
        pump.RunUntilIdle();

        selected.Value = "span";
        pump.RunUntilIdle();

        recorder.Calls.ShouldBe(["beforeEnter", "enter"]);
        harness.Host.Root.Children.ShouldContain(
            node => node.Content == "div");
        harness.Host.Root.Children.ShouldContain(
            node => node.Content == "span");

        recorder.CompleteEnter();
        recorder.Calls.ShouldBe(
        [
            "beforeEnter",
            "enter",
            "afterEnter",
            "beforeLeave",
            "leave",
        ]);
        recorder.CompleteLeave();
        harness.Host.Root.Children.ShouldNotContain(
            node => node.Content == "div");
        harness.Host.Root.Children.ShouldContain(
            node => node.Content == "span");
    }

    private static TransitionHarness Mount(
        BaseTransitionProperties properties,
        Func<IComponent> child)
    {
        ComponentArguments arguments = new(
        [
            new KeyValuePair<string, object?>(
                BaseTransition.PropertiesArgument,
                properties),
        ]);
        Dictionary<string, ComponentSlot> slots = new(StringComparer.Ordinal)
        {
            ["default"] = _ => child(),
        };
        ITemplateComponent root = ComponentTree.Template<BaseTransition>(
            arguments,
            slots);
        ApplicationContext application = new(
            root,
            new ComponentFactory([BaseTransition.Registration]),
            new EmptyServiceProvider());
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        return new TransitionHarness(host, renderer);
    }

    private sealed record TransitionHarness(
        FakeHost Host,
        Renderer<FakeHostNode> Renderer);

    private sealed class TransitionRecorder
    {
        private Action? _enterDone;
        private Action? _leaveDone;

        internal List<string> Calls { get; } = [];

        internal void CompleteEnter()
        {
            Action? done = _enterDone;
            _enterDone = null;
            done?.Invoke();
        }

        internal void CompleteLeave()
        {
            Action? done = _leaveDone;
            _leaveDone = null;
            done?.Invoke();
        }

        internal BaseTransitionProperties Build(
            string? mode = null,
            bool appear = false)
        {
            return new BaseTransitionProperties
            {
                Mode = mode,
                Appear = appear,
                OnBeforeEnter = _ => Calls.Add("beforeEnter"),
                OnEnter = (_, done) =>
                {
                    Calls.Add("enter");
                    _enterDone = done;
                },
                OnAfterEnter = _ => Calls.Add("afterEnter"),
                OnEnterCancelled = _ => Calls.Add("enterCancelled"),
                OnBeforeLeave = _ => Calls.Add("beforeLeave"),
                OnLeave = (_, done) =>
                {
                    Calls.Add("leave");
                    _leaveDone = done;
                },
                OnAfterLeave = _ => Calls.Add("afterLeave"),
                OnLeaveCancelled = _ => Calls.Add("leaveCancelled"),
                OnBeforeAppear = _ => Calls.Add("beforeAppear"),
                OnAppear = (_, done) =>
                {
                    Calls.Add("appear");
                    _enterDone = done;
                },
                OnAfterAppear = _ => Calls.Add("afterAppear"),
                OnAppearCancelled = _ => Calls.Add("appearCancelled"),
            };
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
