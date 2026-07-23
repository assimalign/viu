using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

public sealed class DirectiveRendererTests
{
    [Fact]
    public void Render_DirectiveBinding_InvokesEveryPhaseWithPreviousValue()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> phases = [];
        Directive directive = RecordingDirective(phases);
        IElementComponent initial = DirectedElement(1);
        IApplicationContext application = CreateApplication(
            initial,
            directive);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(initial, host.Root, application);
        pump.RunUntilIdle();
        renderer.Render(DirectedElement(2), host.Root);
        pump.RunUntilIdle();
        renderer.Render(null, host.Root);
        pump.RunUntilIdle();

        phases.ShouldBe(
        [
            "created:1:null",
            "before-mount:1:null",
            "mounted:1:null",
            "before-update:2:1",
            "updated:2:1",
            "before-unmount:2:1",
            "unmounted:2:1",
        ]);
    }

    [Fact]
    public void Render_DirectiveOnTemplateRequest_TransfersToRenderedElementRoot()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        List<string> phases = [];
        Directive directive = RecordingDirective(phases);
        RootTemplate template = new();
        ITemplateComponent root = DirectedTemplate(10);
        ComponentFactory components = new(
        [
            new ComponentRegistration(
                typeof(RootTemplate),
                () => template),
        ]);
        IApplicationContext application = new ApplicationContext(
            root,
            components,
            new EmptyServiceProvider(),
            directives: Registry(directive));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        pump.RunUntilIdle();
        renderer.Render(DirectedTemplate(20), host.Root);
        pump.RunUntilIdle();
        renderer.Render(null, host.Root);
        pump.RunUntilIdle();

        phases.ShouldContain("mounted:10:null");
        phases.ShouldContain("updated:20:10");
        phases.ShouldContain("unmounted:20:10");
    }

    [Fact]
    public void Render_DirectiveHookFault_RoutesToApplicationErrorHandler()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        Directive directive = new()
        {
            Mounted = static (_, _, _, _) =>
                throw new InvalidOperationException("directive failed"),
        };
        IElementComponent root = DirectedElement(1);
        Exception? observed = null;
        string? diagnostic = null;
        IApplicationContext application = CreateApplication(root, directive);
        application.ErrorHandler = (exception, _, information) =>
        {
            observed = exception;
            diagnostic = information;
        };
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        pump.RunUntilIdle();

        observed.ShouldNotBeNull();
        observed.Message.ShouldBe("directive failed");
        diagnostic.ShouldBe("Mounted directive lifecycle hook");
    }

    [Fact]
    public void Render_DirectiveBinding_ExposesMountedDescendantElementsInDocumentOrder()
    {
        using TestSchedulerPump pump = TestSchedulerPump.Install();
        int createdCount = -1;
        int beforeMountCount = -1;
        IReadOnlyList<DirectiveHostElement>? mountedElements = null;
        Directive directive = new()
        {
            Created = (_, binding, _, _) =>
                createdCount = binding.GetDescendantElements("option").Count,
            BeforeMount = (_, binding, _, _) =>
                beforeMountCount = binding.GetDescendantElements("option").Count,
            Mounted = (_, binding, _, _) =>
                mountedElements = binding.GetDescendantElements("option"),
        };
        IElementComponent root = ComponentTree.Element(
            "select",
            children:
            [
                ComponentTree.Element("option"),
                ComponentTree.Fragment(
                [
                    ComponentTree.Element("option"),
                ]),
                ComponentTree.Template<OptionTemplate>(),
            ],
            directives:
            [
                new ComponentDirectiveBinding("record"),
            ]);
        ComponentFactory components = new(
        [
            new ComponentRegistration(
                typeof(OptionTemplate),
                static () => new OptionTemplate()),
        ]);
        IApplicationContext application = new ApplicationContext(
            root,
            components,
            new EmptyServiceProvider(),
            directives: Registry(directive));
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        pump.RunUntilIdle();

        createdCount.ShouldBe(0);
        beforeMountCount.ShouldBe(0);
        mountedElements.ShouldNotBeNull();
        mountedElements.Count.ShouldBe(3);
        for (int index = 0; index < mountedElements.Count; index++)
        {
            mountedElements[index].Component.Tag.ShouldBe("option");
            mountedElements[index].Element
                .ShouldBeOfType<FakeHostNode>()
                .Content
                .ShouldBe("option");
        }
    }

    private static IElementComponent DirectedElement(int value)
    {
        return ComponentTree.Element(
            "div",
            directives:
            [
                new ComponentDirectiveBinding("record", value),
            ]);
    }

    private static ITemplateComponent DirectedTemplate(int value)
    {
        return ComponentTree.Template<RootTemplate>(
            directives:
            [
                new ComponentDirectiveBinding("record", value),
            ]);
    }

    private static Directive RecordingDirective(List<string> phases)
    {
        return new Directive
        {
            Created = (_, binding, _, _) =>
                phases.Add(Describe("created", binding)),
            BeforeMount = (_, binding, _, _) =>
                phases.Add(Describe("before-mount", binding)),
            Mounted = (_, binding, _, _) =>
                phases.Add(Describe("mounted", binding)),
            BeforeUpdate = (_, binding, _, _) =>
                phases.Add(Describe("before-update", binding)),
            Updated = (_, binding, _, _) =>
                phases.Add(Describe("updated", binding)),
            BeforeUnmount = (_, binding, _, _) =>
                phases.Add(Describe("before-unmount", binding)),
            Unmounted = (_, binding, _, _) =>
                phases.Add(Describe("unmounted", binding)),
        };
    }

    private static string Describe(
        string phase,
        DirectiveBinding binding)
    {
        return $"{phase}:{binding.Value}:{binding.PreviousValue ?? "null"}";
    }

    private static IApplicationContext CreateApplication(
        IComponent root,
        IDirective directive)
    {
        return new ApplicationContext(
            root,
            new ComponentFactory(Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider(),
            directives: Registry(directive));
    }

    private static DirectiveRegistry Registry(IDirective directive)
    {
        return new DirectiveRegistry(
        [
            new KeyValuePair<string, IDirective>("record", directive),
        ]);
    }

    private sealed class RootTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element("main");
        }
    }

    private sealed class OptionTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return static () => ComponentTree.Element("option");
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
