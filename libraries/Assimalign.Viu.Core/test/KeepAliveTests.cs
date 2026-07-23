using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins the host-generic KeepAlive cache against Vue 3.5's observable lifecycle, filtering, and
/// least-recently-used behavior.
/// </summary>
/// <remarks>
/// Upstream contract:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/__tests__/components/KeepAlive.spec.ts.
/// </remarks>
public sealed class KeepAliveTests : IDisposable
{
    private readonly TestSchedulerPump _pump;

    public KeepAliveTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    [Fact]
    public void Render_SwitchAwayAndBack_PreservesStateAndSetupRunsOnce()
    {
        KeepAliveTestState state = new();
        Reference<string> selected = Reactive.Reference("a");
        KeepAliveHarness harness = MountKeepAlive(
            state,
            selected);

        state.Values["a"].Value = 5;
        _pump.RunUntilIdle();
        harness.Host.Text(harness.Host.Root).ShouldBe("a5");

        selected.Value = "b";
        _pump.RunUntilIdle();
        harness.Host.Text(harness.Host.Root).ShouldBe("b0");

        selected.Value = "a";
        _pump.RunUntilIdle();

        harness.Host.Text(harness.Host.Root).ShouldBe("a5");
        state.SetupCounts["a"].ShouldBe(1);
        state.SetupCounts["b"].ShouldBe(1);
        state.Events.ShouldNotContain("a:unmounted");
        state.Events.ShouldNotContain("b:unmounted");
    }

    [Fact]
    public void Render_InitialMountAndSwitches_InvokeActivatedAndDeactivatedInOrder()
    {
        KeepAliveTestState state = new();
        Reference<string> selected = Reactive.Reference("a");
        MountKeepAlive(state, selected);

        selected.Value = "b";
        _pump.RunUntilIdle();
        selected.Value = "a";
        _pump.RunUntilIdle();

        state.Events.ShouldBe(
        [
            "a:mounted",
            "a:activated",
            "a:deactivated",
            "b:mounted",
            "b:activated",
            "b:deactivated",
            "a:activated",
        ]);
        state.Events.Count(entry => entry == "a:mounted").ShouldBe(1);
        state.Events.Count(entry => entry == "a:activated").ShouldBe(2);
        state.Events.Count(entry => entry == "a:deactivated").ShouldBe(1);
    }

    [Fact]
    public void Render_IncludeAndExclude_UncachedTemplateUnmountsAndRemounts()
    {
        KeepAliveTestState state = new();
        Reference<string> selected = Reactive.Reference("a");
        MountKeepAlive(
            state,
            selected,
            include: "a",
            exclude: "b");

        selected.Value = "b";
        _pump.RunUntilIdle();
        selected.Value = "a";
        _pump.RunUntilIdle();
        selected.Value = "b";
        _pump.RunUntilIdle();

        state.SetupCounts["a"].ShouldBe(1);
        state.Events.ShouldNotContain("a:unmounted");
        state.SetupCounts["b"].ShouldBe(2);
        state.Events.Count(entry => entry == "b:unmounted").ShouldBe(1);
        state.Events.ShouldNotContain("b:activated");
        state.Events.ShouldNotContain("b:deactivated");
    }

    [Fact]
    public void ShouldCache_StringSequenceAndPredicate_MatchComponentNames()
    {
        KeepAlive keepAlive = new();
        Func<string, bool> namedTemplate =
            name => name.EndsWith(
                "Template",
                StringComparison.Ordinal);

        keepAlive.ShouldCache(
                Arguments(("include", "a,b")),
                "b")
            .ShouldBeTrue();
        keepAlive.ShouldCache(
                Arguments(("include", new List<string> { "a", "b" })),
                "b")
            .ShouldBeTrue();
        keepAlive.ShouldCache(
                Arguments(("include", namedTemplate)),
                "NamedTemplate")
            .ShouldBeTrue();
        keepAlive.ShouldCache(
                Arguments(("exclude", "b")),
                "b")
            .ShouldBeFalse();
    }

    [Fact]
    public void Render_Maximum_EvictsLeastRecentlyUsedTemplate()
    {
        KeepAliveTestState state = new();
        Reference<string> selected = Reactive.Reference("a");
        MountKeepAlive(
            state,
            selected,
            maximum: 2);

        selected.Value = "b";
        _pump.RunUntilIdle();
        selected.Value = "c";
        _pump.RunUntilIdle();

        state.Events.Count(entry => entry == "a:unmounted").ShouldBe(1);
        state.Events.Count(entry => entry == "a:deactivated").ShouldBe(1);
        state.Events.ShouldNotContain("b:unmounted");

        selected.Value = "a";
        _pump.RunUntilIdle();

        state.SetupCounts["a"].ShouldBe(2);
    }

    [Fact]
    public void Render_ChangedInclude_PrunesNewlyExcludedCachedTemplate()
    {
        KeepAliveTestState state = new();
        Reference<string> selected = Reactive.Reference("a");
        Reference<object?> include = Reactive.Reference<object?>(null);
        ITemplateComponent root = ComponentTree.Template<FilterRootTemplate>();
        ComponentFactory factory = new(
        [
            KeepAlive.Registration,
            new ComponentRegistration(
                typeof(FilterRootTemplate),
                () => new FilterRootTemplate(selected, include)),
            .. Registrations(state),
        ]);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        IApplicationContext application = new ApplicationContext(
            root,
            factory,
            new EmptyServiceProvider());
        renderer.Render(root, host.Root, application);
        _pump.RunUntilIdle();

        selected.Value = "b";
        _pump.RunUntilIdle();
        state.SetupCounts["a"].ShouldBe(1);

        include.Value = "b";
        _pump.RunUntilIdle();
        state.Events.Count(entry => entry == "a:unmounted").ShouldBe(1);

        selected.Value = "a";
        _pump.RunUntilIdle();
        state.SetupCounts["a"].ShouldBe(2);
    }

    [Fact]
    public void Render_UnmountKeepAlive_UnmountsActiveAndCachedTemplates()
    {
        KeepAliveTestState state = new();
        Reference<string> selected = Reactive.Reference("a");
        KeepAliveHarness harness = MountKeepAlive(state, selected);

        selected.Value = "b";
        _pump.RunUntilIdle();
        state.Events.Clear();

        harness.Renderer.Render(null, harness.Host.Root);
        _pump.RunUntilIdle();

        state.Events.Count(entry => entry == "a:unmounted").ShouldBe(1);
        state.Events.Count(entry => entry == "b:unmounted").ShouldBe(1);
        harness.Host.Root.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Render_NestedTemplate_InvokesKeepAliveLifecycleChildBeforeParent()
    {
        KeepAliveTestState state = new();
        Reference<string> selected = Reactive.Reference("outer");
        ITemplateComponent root = KeepAlive.CreateComponent(
            include: null,
            exclude: null,
            maximum: null,
            _ => selected.Value == "outer"
                ? ComponentTree.Template<OuterTemplate>()
                : ComponentTree.Template<ThirdTemplate>());
        ComponentFactory factory = new(
        [
            KeepAlive.Registration,
            new ComponentRegistration(
                typeof(OuterTemplate),
                () => new OuterTemplate(state)),
            new ComponentRegistration(
                typeof(InnerTemplate),
                () => new InnerTemplate(state)),
            new ComponentRegistration(
                typeof(ThirdTemplate),
                () => new ThirdTemplate(state)),
        ]);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            root,
            host.Root,
            new ApplicationContext(
                root,
                factory,
                new EmptyServiceProvider()));
        _pump.RunUntilIdle();

        selected.Value = "third";
        _pump.RunUntilIdle();
        selected.Value = "outer";
        _pump.RunUntilIdle();

        state.Events
            .Where(entry =>
                entry.StartsWith("inner:", StringComparison.Ordinal)
                || entry.StartsWith("outer:", StringComparison.Ordinal))
            .ShouldBe(
            [
                "inner:mounted",
                "outer:mounted",
                "inner:activated",
                "outer:activated",
                "inner:deactivated",
                "outer:deactivated",
                "inner:activated",
                "outer:activated",
            ]);
    }

    [Fact]
    public void Render_CacheSwitch_InvokesComponentNodeMountedAndUnmountedHooks()
    {
        KeepAliveTestState state = new();
        List<string> nodeEvents = [];
        Reference<string> selected = Reactive.Reference("a");
        ITemplateComponent root = KeepAlive.CreateComponent(
            include: null,
            exclude: null,
            maximum: null,
            _ => ComponentWithNodeHooks(selected.Value, nodeEvents));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            root,
            host.Root,
            new ApplicationContext(
                root,
                new ComponentFactory(
                [
                    KeepAlive.Registration,
                    .. Registrations(state),
                ]),
                new EmptyServiceProvider()));
        _pump.RunUntilIdle();

        selected.Value = "b";
        _pump.RunUntilIdle();
        selected.Value = "a";
        _pump.RunUntilIdle();

        nodeEvents.ShouldBe(
        [
            "a:mounted",
            "a:unmounted",
            "b:mounted",
            "b:unmounted",
            "a:mounted",
        ]);
    }

    [Fact]
    public void Render_WithoutKeepAlive_DoesNotInvokeActivatedOrDeactivated()
    {
        KeepAliveTestState state = new();
        ITemplateComponent root = ComponentTree.Template<FirstTemplate>();
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            root,
            host.Root,
            new ApplicationContext(
                root,
                new ComponentFactory(
                [
                    new ComponentRegistration(
                        typeof(FirstTemplate),
                        () => new FirstTemplate(state)),
                ]),
                new EmptyServiceProvider()));
        _pump.RunUntilIdle();

        renderer.Render(null, host.Root);
        _pump.RunUntilIdle();

        state.Events.ShouldBe(["a:mounted", "a:unmounted"]);
    }

    private KeepAliveHarness MountKeepAlive(
        KeepAliveTestState state,
        Reference<string> selected,
        object? include = null,
        object? exclude = null,
        object? maximum = null)
    {
        ITemplateComponent root = KeepAlive.CreateComponent(
            include,
            exclude,
            maximum,
            _ => SelectedComponent(selected.Value));
        ComponentFactory factory = new(
        [
            KeepAlive.Registration,
            .. Registrations(state),
        ]);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        renderer.Render(
            root,
            host.Root,
            new ApplicationContext(
                root,
                factory,
                new EmptyServiceProvider()));
        _pump.RunUntilIdle();
        return new KeepAliveHarness(host, renderer);
    }

    private static ITemplateComponent SelectedComponent(string selected)
    {
        return selected switch
        {
            "a" => ComponentTree.Template<FirstTemplate>(),
            "b" => ComponentTree.Template<SecondTemplate>(),
            _ => ComponentTree.Template<ThirdTemplate>(),
        };
    }

    private static ITemplateComponent ComponentWithNodeHooks(
        string selected,
        List<string> events)
    {
        Type templateType = selected == "a"
            ? typeof(FirstTemplate)
            : typeof(SecondTemplate);
        ComponentNodeLifecycleHook mounted =
            (_, _) => events.Add($"{selected}:mounted");
        ComponentNodeLifecycleHook unmounted =
            (_, _) => events.Add($"{selected}:unmounted");
        return ComponentTree.Template(
            templateType,
            Arguments(
                ("onVnodeMounted", mounted),
                ("onVnodeUnmounted", unmounted)));
    }

    private static ComponentRegistration[] Registrations(
        KeepAliveTestState state)
    {
        return
        [
            new ComponentRegistration(
                typeof(FirstTemplate),
                () => new FirstTemplate(state)),
            new ComponentRegistration(
                typeof(SecondTemplate),
                () => new SecondTemplate(state)),
            new ComponentRegistration(
                typeof(ThirdTemplate),
                () => new ThirdTemplate(state)),
        ];
    }

    private static ComponentArguments Arguments(
        params (string Name, object? Value)[] arguments)
    {
        List<KeyValuePair<string, object?>> values =
            new(arguments.Length);
        for (int index = 0; index < arguments.Length; index++)
        {
            values.Add(
                new KeyValuePair<string, object?>(
                    arguments[index].Name,
                    arguments[index].Value));
        }

        return new ComponentArguments(values);
    }

    private sealed record KeepAliveHarness(
        FakeHost Host,
        Renderer<FakeHostNode> Renderer);

    private sealed class KeepAliveTestState
    {
        internal List<string> Events { get; } = [];

        internal Dictionary<string, int> SetupCounts { get; } =
            new(StringComparer.Ordinal);

        internal Dictionary<string, Reference<int>> Values { get; } =
            new(StringComparer.Ordinal);

        internal void RecordSetup(string name, Reference<int> value)
        {
            SetupCounts[name] = SetupCounts.GetValueOrDefault(name) + 1;
            Values[name] = value;
        }
    }

    private abstract class TrackedTemplate : IComponentTemplate
    {
        private readonly KeepAliveTestState _state;
        private readonly string _name;

        protected TrackedTemplate(
            KeepAliveTestState state,
            string name)
        {
            _state = state;
            _name = name;
        }

        public string? Name => _name;

        public ComponentRenderer Setup(IComponentContext context)
        {
            Reference<int> value = Reactive.Reference(0);
            _state.RecordSetup(_name, value);
            context.Lifecycle.OnMounted(
                () => _state.Events.Add($"{_name}:mounted"));
            context.Lifecycle.OnUnmounted(
                () => _state.Events.Add($"{_name}:unmounted"));
            context.Lifecycle.OnActivated(
                () => _state.Events.Add($"{_name}:activated"));
            context.Lifecycle.OnDeactivated(
                () => _state.Events.Add($"{_name}:deactivated"));
            return () => ComponentTree.Element(
                "div",
                children:
                [
                    ComponentTree.Text($"{_name}{value.Value}"),
                ]);
        }
    }

    private sealed class FirstTemplate : TrackedTemplate
    {
        internal FirstTemplate(KeepAliveTestState state)
            : base(state, "a")
        {
        }
    }

    private sealed class SecondTemplate : TrackedTemplate
    {
        internal SecondTemplate(KeepAliveTestState state)
            : base(state, "b")
        {
        }
    }

    private sealed class ThirdTemplate : TrackedTemplate
    {
        internal ThirdTemplate(KeepAliveTestState state)
            : base(state, "c")
        {
        }
    }

    private sealed class FilterRootTemplate : IComponentTemplate
    {
        private readonly Reference<object?> _include;
        private readonly Reference<string> _selected;

        internal FilterRootTemplate(
            Reference<string> selected,
            Reference<object?> include)
        {
            _selected = selected;
            _include = include;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () => KeepAlive.CreateComponent(
                _include.Value,
                exclude: null,
                maximum: null,
                _ => SelectedComponent(_selected.Value));
        }
    }

    private sealed class OuterTemplate : IComponentTemplate
    {
        private readonly KeepAliveTestState _state;

        internal OuterTemplate(KeepAliveTestState state)
        {
            _state = state;
        }

        public string? Name => "outer";

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(
                () => _state.Events.Add("outer:mounted"));
            context.Lifecycle.OnActivated(
                () => _state.Events.Add("outer:activated"));
            context.Lifecycle.OnDeactivated(
                () => _state.Events.Add("outer:deactivated"));
            return static () => ComponentTree.Element(
                "div",
                children:
                [
                    ComponentTree.Template<InnerTemplate>(),
                ]);
        }
    }

    private sealed class InnerTemplate : IComponentTemplate
    {
        private readonly KeepAliveTestState _state;

        internal InnerTemplate(KeepAliveTestState state)
        {
            _state = state;
        }

        public string? Name => "inner";

        public ComponentRenderer Setup(IComponentContext context)
        {
            context.Lifecycle.OnMounted(
                () => _state.Events.Add("inner:mounted"));
            context.Lifecycle.OnActivated(
                () => _state.Events.Add("inner:activated"));
            context.Lifecycle.OnDeactivated(
                () => _state.Events.Add("inner:deactivated"));
            return static () => ComponentTree.Element(
                "span",
                children:
                [
                    ComponentTree.Text("inner"),
                ]);
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
