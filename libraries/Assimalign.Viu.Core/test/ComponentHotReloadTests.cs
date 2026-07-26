using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

public sealed class ComponentHotReloadTests : IDisposable
{
    public ComponentHotReloadTests()
    {
        ComponentHotReload.Reset();
    }

    [Fact]
    public void ApplyUpdates_TemplateOnlyChange_RemountsWithUpdatedGeneratedCode()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HotReloadSource source = new();
        ITemplateComponent request = ComponentTree.Template<HotReloadTemplate>();
        IApplicationContext application = CreateApplication(source, request);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(request, host.Root, application);
        pump.RunUntilIdle();
        HotReloadTemplate previousInstance = source.Instances.Single();
        previousInstance.State = 7;
        source.RenderedLabel = "after";

        ComponentHotReload.ApplyUpdates(
        [
            typeof(HotReloadTemplate.TemplateUpdateMarker),
            typeof(HotReloadTemplate),
        ]);
        pump.RunUntilIdle();

        source.Instances.Count.ShouldBe(2);
        HotReloadTemplate nextInstance = source.Instances[1];
        nextInstance.ShouldNotBeSameAs(previousInstance);
        previousInstance.IsDisposed.ShouldBeTrue();
        previousInstance.ScopeWasDisposed.ShouldBeTrue();
        previousInstance.RenderCount.ShouldBe(1);
        nextInstance.SetupCount.ShouldBe(1);
        nextInstance.State.ShouldBe(0);
        host.Text(host.Root).ShouldBe("after:0");

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();
    }

    [Fact]
    public void ApplyUpdates_StyleOnlyChange_DoesNoComponentWork()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HotReloadSource source = new();
        ITemplateComponent request = ComponentTree.Template<HotReloadTemplate>();
        IApplicationContext application = CreateApplication(source, request);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(request, host.Root, application);
        pump.RunUntilIdle();
        HotReloadTemplate instance = source.Instances.Single();
        source.RenderedLabel = "must-not-render";

        ComponentHotReload.ApplyUpdates(
        [
            typeof(HotReloadTemplate.StyleUpdateMarker),
            typeof(HotReloadTemplate),
        ]);
        pump.RunUntilIdle();

        instance.RenderCount.ShouldBe(1);
        source.Instances.Count.ShouldBe(1);
        host.Text(host.Root).ShouldBe("before:0");

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();
    }

    [Fact]
    public void ApplyUpdates_MixedBlockChange_ScriptResetTakesPrecedenceAndResetsInPlace()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HotReloadSource source = new();
        ITemplateComponent request = ComponentTree.Template<HotReloadTemplate>();
        IApplicationContext application = CreateApplication(source, request);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);
        List<(string Identifier, Type Type)> resetNotifications = [];
        ComponentHotReload.ScriptUpdateRequiresReset +=
            (identifier, type) =>
                resetNotifications.Add((identifier, type));

        renderer.Render(request, host.Root, application);
        pump.RunUntilIdle();
        HotReloadTemplate previousInstance = source.Instances.Single();
        previousInstance.State = 9;
        source.RenderedLabel = "script-after";

        ComponentHotReload.ApplyUpdates(
        [
            typeof(HotReloadTemplate.TemplateUpdateMarker),
            typeof(HotReloadTemplate.ScriptUpdateMarker),
            typeof(HotReloadTemplate.StyleUpdateMarker),
            typeof(HotReloadTemplate),
        ]);
        pump.RunUntilIdle();

        resetNotifications.ShouldBe(
        [
            ("component:test", typeof(HotReloadTemplate)),
        ]);
        source.Instances.Count.ShouldBe(2);
        HotReloadTemplate nextInstance = source.Instances[1];
        nextInstance.ShouldNotBeSameAs(previousInstance);
        previousInstance.IsDisposed.ShouldBeTrue();
        previousInstance.ScopeWasDisposed.ShouldBeTrue();
        previousInstance.RenderCount.ShouldBe(1);
        nextInstance.SetupCount.ShouldBe(1);
        nextInstance.State.ShouldBe(0);
        host.Text(host.Root).ShouldBe("script-after:0");

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();
    }

    [Fact]
    public void ApplyUpdates_HydratedScriptChange_ResetsTheRegisteredComponentInPlace()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HotReloadSource source = new();
        ITemplateComponent request = ComponentTree.Template<HotReloadTemplate>();
        IApplicationContext application = CreateApplication(source, request);
        FakeHost host = new();
        FakeHostNode serverText = host.CreateServerText("before:0");
        host.AppendServerChild(host.Root, serverText);
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(request, host.Root, application);
        pump.RunUntilIdle();
        host.Root.Children.Single().ShouldBeSameAs(serverText);
        HotReloadTemplate previousInstance = source.Instances.Single();
        source.RenderedLabel = "hydrated-script-after";

        ComponentHotReload.ApplyUpdates(
        [
            typeof(HotReloadTemplate.ScriptUpdateMarker),
            typeof(HotReloadTemplate),
        ]);
        pump.RunUntilIdle();

        source.Instances.Count.ShouldBe(2);
        previousInstance.IsDisposed.ShouldBeTrue();
        previousInstance.ScopeWasDisposed.ShouldBeTrue();
        host.Text(host.Root).ShouldBe("hydrated-script-after:0");

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();
    }

    [Fact]
    public void ApplyUpdates_UnchangedAndUnknownTypes_DoNothing()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HotReloadSource source = new();
        ITemplateComponent request = ComponentTree.Template<HotReloadTemplate>();
        IApplicationContext application = CreateApplication(source, request);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(request, host.Root, application);
        pump.RunUntilIdle();
        HotReloadTemplate instance = source.Instances.Single();
        source.RenderedLabel = "must-not-render";

        ComponentHotReload.ApplyUpdates(
        [
            typeof(string),
            typeof(int),
        ]);
        pump.RunUntilIdle();

        instance.RenderCount.ShouldBe(1);
        source.Instances.Count.ShouldBe(1);
        host.Text(host.Root).ShouldBe("before:0");

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();
    }

    [Fact]
    public void ApplyUpdates_NullTypes_ConservativelyRemountsEveryRegisteredComponent()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HotReloadSource source = new();
        ITemplateComponent request = ComponentTree.Template<HotReloadTemplate>();
        IApplicationContext application = CreateApplication(source, request);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(request, host.Root, application);
        pump.RunUntilIdle();
        HotReloadTemplate previousInstance = source.Instances.Single();
        source.RenderedLabel = "all";

        ComponentHotReload.ApplyUpdates(updatedTypes: null);
        pump.RunUntilIdle();

        source.Instances.Count.ShouldBe(2);
        previousInstance.IsDisposed.ShouldBeTrue();
        source.Instances[1].ShouldNotBeSameAs(previousInstance);
        host.Text(host.Root).ShouldBe("all:0");

        renderer.Render(null, host.Root);
        pump.RunUntilIdle();
    }

    [Fact]
    public void ApplyUpdates_UnmountedComponent_IsNotRetainedOrRendered()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        HotReloadSource source = new();
        ITemplateComponent request = ComponentTree.Template<HotReloadTemplate>();
        IApplicationContext application = CreateApplication(source, request);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(request, host.Root, application);
        pump.RunUntilIdle();
        HotReloadTemplate instance = source.Instances.Single();
        renderer.Render(null, host.Root);
        pump.RunUntilIdle();

        ComponentHotReload.ApplyUpdates(
        [
            typeof(HotReloadTemplate.TemplateUpdateMarker),
            typeof(HotReloadTemplate),
        ]);
        pump.RunUntilIdle();

        instance.RenderCount.ShouldBe(1);
        source.Instances.Count.ShouldBe(1);
        host.Root.Children.ShouldBeEmpty();
    }

    public void Dispose()
    {
        ComponentHotReload.Reset();
        Scheduler.Reset();
    }

    private static IApplicationContext CreateApplication(
        HotReloadSource source,
        ITemplateComponent request)
    {
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(HotReloadTemplate),
                source.Create),
        ]);
        return new ApplicationContext(
            request,
            factory,
            new EmptyServiceProvider());
    }

    private sealed class HotReloadSource
    {
        internal string ComponentIdentifier { get; set; } = "component:test";

        internal string RenderedLabel { get; set; } = "before";

        internal List<HotReloadTemplate> Instances { get; } = [];

        internal IComponentTemplate Create()
        {
            HotReloadTemplate instance = new(this);
            Instances.Add(instance);
            return instance;
        }
    }

    private sealed class HotReloadTemplate :
        IComponentTemplate,
        IComponentHotReloadMetadata,
        IDisposable
    {
        private readonly HotReloadSource _source;
        private string? _renderedLabelCache;

        internal HotReloadTemplate(HotReloadSource source)
        {
            _source = source;
        }

        public string ComponentIdentifier => _source.ComponentIdentifier;

        public Type TemplateUpdateMarkerType => typeof(TemplateUpdateMarker);

        public Type ScriptUpdateMarkerType => typeof(ScriptUpdateMarker);

        public Type StyleUpdateMarkerType => typeof(StyleUpdateMarker);

        internal int State { get; set; }

        internal int SetupCount { get; private set; }

        internal int RenderCount { get; private set; }

        internal bool IsDisposed { get; private set; }

        internal bool ScopeWasDisposed { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            SetupCount++;
            Reactive.OnScopeDispose(() => ScopeWasDisposed = true);
            return () => (IComponent)RenderTemplate();
        }

        private object RenderTemplate()
        {
            RenderCount++;
            _renderedLabelCache ??= _source.RenderedLabel;
            return ComponentTree.Text(
                $"{_renderedLabelCache}:{State}");
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        internal static class TemplateUpdateMarker
        {
        }

        internal static class ScriptUpdateMarker
        {
        }

        internal static class StyleUpdateMarker
        {
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
