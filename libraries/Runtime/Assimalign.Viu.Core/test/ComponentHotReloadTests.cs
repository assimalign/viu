using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

public sealed class ComponentHotReloadTests
{
    [Fact]
    public void Classify_RegisteredMarkerSets_UsesConservativePrecedence()
    {
        ComponentHotReload.Register(
            typeof(ClassificationComponent),
            "classification-component",
            typeof(ClassificationTemplateMarker),
            typeof(ClassificationScriptMarker),
            typeof(ClassificationStyleMarker));

        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationStyleMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.StyleOnly);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationStyleMarker), typeof(ClassificationTemplateMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.Template);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationTemplateMarker), typeof(ClassificationScriptMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.ScriptReset);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationComponent), typeof(ClassificationStyleMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.StyleOnly);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationComponent), typeof(ClassificationTemplateMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.Template);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationComponent), typeof(ClassificationScriptMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.ScriptReset);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(ClassificationComponent)])
            .ShouldBe(ComponentHotReloadChangeKind.ScriptReset);
        ComponentHotReload.Classify(typeof(ClassificationComponent), null)
            .ShouldBe(ComponentHotReloadChangeKind.ScriptReset);
        ComponentHotReload.Classify(
            typeof(ClassificationComponent),
            [typeof(UnrelatedMarker)])
            .ShouldBe(ComponentHotReloadChangeKind.None);
    }

    [Fact]
    public void ApplyUpdates_TemplateChange_RemountsWithFreshStateAndUpdatedRenderCode()
    {
        using var host = new RendererParityHost();
        HotReloadProbeSource source = new();
        ComponentNode request = CreateProbeRequest();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RegisterProbeMetadata();

        renderer.Render(request, host.Container, CreateProbeApplication(request, source));
        HotReloadProbeComponent previous = source.Instances.Single();
        previous.State = 7;
        source.Label = "after";

        ComponentHotReload.ApplyUpdates([typeof(ProbeTemplateMarker)]);
        host.RunScheduledFlushes();

        source.Instances.Count.ShouldBe(2);
        HotReloadProbeComponent next = source.Instances[1];
        next.ShouldNotBeSameAs(previous);
        previous.IsDisposed.ShouldBeTrue();
        previous.ScopeWasDisposed.ShouldBeTrue();
        previous.RenderCount.ShouldBe(1);
        next.SetupCount.ShouldBe(1);
        next.State.ShouldBe(0);
        host.Container.DescendantText.ShouldBe("after:0");

        renderer.Render(null, host.Container);
    }

    [Fact]
    public void ApplyUpdates_TemplateChange_RemountsPostFlushAndCommitsOnce()
    {
        using var host = new RendererParityHost();
        HotReloadProbeSource source = new();
        ComponentNode request = CreateProbeRequest();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RegisterProbeMetadata();
        renderer.Render(request, host.Container, CreateProbeApplication(request, source));
        host.ResetOperationCounts();
        int instancesObservedBeforeRemount = 0;
        int commitsObservedBeforeRemount = 0;

        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    instancesObservedBeforeRemount = source.Instances.Count;
                    commitsObservedBeforeRemount = host.CommitCount;
                })
            {
                Name = "hot-reload post-flush ordering probe",
            });
        Scheduler.QueueJob(
            new SchedulerJob(
                () => ComponentHotReload.ApplyUpdates([typeof(ProbeTemplateMarker)]))
            {
                Name = "hot-reload update application probe",
            });

        host.RunScheduledFlushes();

        instancesObservedBeforeRemount.ShouldBe(1);
        commitsObservedBeforeRemount.ShouldBe(0);
        source.Instances.Count.ShouldBe(2);
        host.CommitCount.ShouldBe(1);

        renderer.Render(null, host.Container);
    }

    [Fact]
    public void ApplyUpdates_StyleOnlyChange_DoesNoMountedComponentWork()
    {
        using var host = new RendererParityHost();
        HotReloadProbeSource source = new();
        ComponentNode request = CreateProbeRequest();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RegisterProbeMetadata();
        renderer.Render(request, host.Container, CreateProbeApplication(request, source));
        HotReloadProbeComponent instance = source.Instances.Single();
        source.Label = "must-not-render";

        ComponentHotReload.ApplyUpdates([typeof(ProbeStyleMarker)]);
        host.RunScheduledFlushes();

        source.Instances.ShouldHaveSingleItem();
        instance.RenderCount.ShouldBe(1);
        host.Container.DescendantText.ShouldBe("before:0");

        renderer.Render(null, host.Container);
    }

    [Fact]
    public void ApplyUpdates_MixedMarkers_ScriptResetTakesPrecedenceAndRemountsOnce()
    {
        using var host = new RendererParityHost();
        HotReloadProbeSource source = new();
        ComponentNode request = CreateProbeRequest();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RegisterProbeMetadata();
        renderer.Render(request, host.Container, CreateProbeApplication(request, source));
        HotReloadProbeComponent previous = source.Instances.Single();
        previous.State = 9;
        source.Label = "script-after";

        ComponentHotReload.ApplyUpdates(
        [
            typeof(ProbeStyleMarker),
            typeof(ProbeTemplateMarker),
            typeof(ProbeScriptMarker),
        ]);
        host.RunScheduledFlushes();

        source.Instances.Count.ShouldBe(2);
        previous.IsDisposed.ShouldBeTrue();
        previous.ScopeWasDisposed.ShouldBeTrue();
        host.Container.DescendantText.ShouldBe("script-after:0");

        renderer.Render(null, host.Container);
    }

    [Fact]
    public void ApplyUpdates_UnknownMarkersDoNothingAndNullConservativelyRemounts()
    {
        using var host = new RendererParityHost();
        HotReloadProbeSource source = new();
        ComponentNode request = CreateProbeRequest();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RegisterProbeMetadata();
        renderer.Render(request, host.Container, CreateProbeApplication(request, source));
        HotReloadProbeComponent initial = source.Instances.Single();
        source.Label = "all";

        ComponentHotReload.ApplyUpdates([typeof(UnrelatedMarker)]);
        host.RunScheduledFlushes();

        source.Instances.ShouldHaveSingleItem();
        initial.RenderCount.ShouldBe(1);
        host.Container.DescendantText.ShouldBe("before:0");

        ComponentHotReload.ApplyUpdates(updatedTypes: null);
        host.RunScheduledFlushes();

        source.Instances.Count.ShouldBe(2);
        initial.IsDisposed.ShouldBeTrue();
        host.Container.DescendantText.ShouldBe("all:0");

        renderer.Render(null, host.Container);
    }

    [Fact]
    public void ApplyUpdates_UnmountedComponent_IsNotRetainedOrRendered()
    {
        using var host = new RendererParityHost();
        HotReloadProbeSource source = new();
        ComponentNode request = CreateProbeRequest();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        RegisterProbeMetadata();
        renderer.Render(request, host.Container, CreateProbeApplication(request, source));
        HotReloadProbeComponent instance = source.Instances.Single();
        renderer.Render(null, host.Container);

        ComponentHotReload.ApplyUpdates([typeof(ProbeTemplateMarker)]);
        host.RunScheduledFlushes();

        source.Instances.ShouldHaveSingleItem();
        instance.RenderCount.ShouldBe(1);
        host.Container.Children.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyUpdates_HydratedScriptChange_RemountsRegisteredComponent()
    {
        Scheduler.Reset();
        Queue<Action> scheduledFlushes = [];
        using IDisposable registration = Scheduler.UseFlushDispatcher(scheduledFlushes.Enqueue);
        try
        {
            HydrationWalkerFakeHost host = new();
            HydrationWalkerHostNode serverText = host.CreateServerText("before:0");
            host.AppendServerChild(host.Root, serverText);
            HotReloadProbeSource source = new();
            ComponentNode request = CreateProbeRequest();
            Renderer<HydrationWalkerHostNode> renderer =
                RendererFactory.CreateRenderer(host.Options);
            RegisterProbeMetadata();
            renderer.Hydrate(request, host.Root, CreateProbeApplication(request, source));
            HotReloadProbeComponent previous = source.Instances.Single();
            source.Label = "hydrated-after";

            ComponentHotReload.ApplyUpdates([typeof(ProbeScriptMarker)]);
            while (scheduledFlushes.Count > 0)
            {
                scheduledFlushes.Dequeue()();
            }

            source.Instances.Count.ShouldBe(2);
            previous.IsDisposed.ShouldBeTrue();
            previous.ScopeWasDisposed.ShouldBeTrue();
            serverText.Parent.ShouldBeNull();
            host.Root.Children.ShouldHaveSingleItem().Data.ShouldBe("hydrated-after:0");

            renderer.Render(null, host.Root);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    private static void RegisterProbeMetadata()
    {
        ComponentHotReload.Register(
            typeof(HotReloadProbeComponent),
            "hot-reload-probe",
            typeof(ProbeTemplateMarker),
            typeof(ProbeScriptMarker),
            typeof(ProbeStyleMarker));
    }

    private static ComponentNode CreateProbeRequest() =>
        new(ComponentReference.ForType(typeof(HotReloadProbeComponent)));

    private static ApplicationContext CreateProbeApplication(
        ComponentNode root,
        HotReloadProbeSource source)
    {
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                root.Component,
                new ComponentContract(),
                _ => source.Create()));
        return new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
            });
    }

    private sealed class ClassificationComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }

    private sealed class HotReloadProbeSource
    {
        internal string Label { get; set; } = "before";

        internal List<HotReloadProbeComponent> Instances { get; } = [];

        internal HotReloadProbeComponent Create()
        {
            var instance = new HotReloadProbeComponent(this);
            Instances.Add(instance);
            return instance;
        }
    }

    private sealed class HotReloadProbeComponent : IComponent, IDisposable
    {
        private readonly HotReloadProbeSource _source;

        internal HotReloadProbeComponent(HotReloadProbeSource source)
        {
            _source = source;
        }

        internal int State { get; set; }

        internal int SetupCount { get; private set; }

        internal int RenderCount { get; private set; }

        internal bool IsDisposed { get; private set; }

        internal bool ScopeWasDisposed { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            _ = context;
            SetupCount++;
            Reactive.OnScopeDispose(() => ScopeWasDisposed = true);
            return _ =>
            {
                RenderCount++;
                return new TextNode($"{_source.Label}:{State}");
            };
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class ClassificationTemplateMarker
    {
    }

    private sealed class ClassificationScriptMarker
    {
    }

    private sealed class ClassificationStyleMarker
    {
    }

    private sealed class UnrelatedMarker
    {
    }

    private sealed class ProbeTemplateMarker
    {
    }

    private sealed class ProbeScriptMarker
    {
    }

    private sealed class ProbeStyleMarker
    {
    }
}
