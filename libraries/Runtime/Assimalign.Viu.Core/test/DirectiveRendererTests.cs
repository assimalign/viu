using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class DirectiveRendererTests
{
    [Fact]
    public void Render_Directive_InvokesEveryPhaseWithPreviousValue()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        List<string> phases = [];
        Directive directive = RecordingDirective(phases);
        ElementNode initial = DirectedElement(1);
        ApplicationContext application = CreateApplication(initial, directive);

        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        renderer.Render(DirectedElement(2), host.Container);
        host.RunScheduledFlushes();
        renderer.Render(null, host.Container);
        host.RunScheduledFlushes();

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
    public void Render_NodeLifecycleBindings_InvokesRendererPhasesAndNeverHostPatches()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        List<string> phases = [];

        renderer.Render(LifecycleElement("first", phases), host.Container);
        host.RunScheduledFlushes();
        host.ResetOperationCounts();
        renderer.Render(LifecycleElement("second", phases), host.Container);
        host.RunScheduledFlushes();
        renderer.Render(null, host.Container);
        host.RunScheduledFlushes();

        phases.ShouldBe(
        [
            "before-mount:first:null",
            "mounted:first:null",
            "before-update:second:first",
            "updated:second:first",
            "before-unmount:second:null",
            "unmounted:second:null",
        ]);
        host.BindingPatchCount.ShouldBe(0);
    }

    [Fact]
    public void Render_DirectiveHookFault_RoutesToApplicationErrorHandler()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        Exception? observed = null;
        string? diagnosticInformation = null;
        var directive = new Directive
        {
            Mounted = static (_, _, _, _) =>
                throw new InvalidOperationException("directive failed"),
        };
        ElementNode root = DirectedElement(1);
        ApplicationContext application = CreateApplication(
            root,
            directive,
            (exception, _, information) =>
            {
                observed = exception;
                diagnosticInformation = information;
            });

        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();

        observed.ShouldNotBeNull().Message.ShouldBe("directive failed");
        diagnosticInformation.ShouldBe("Mounted directive lifecycle hook");
    }

    [Fact]
    public void Render_DirectiveMountedHook_SeesDescendantElementsInTreeOrder()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        IReadOnlyList<DirectiveHostElement>? descendants = null;
        var directive = new Directive
        {
            Created = (_, binding, _, _) =>
                binding.GetDescendantElements("option").ShouldBeEmpty(),
            BeforeMount = (_, binding, _, _) =>
                binding.GetDescendantElements("option").ShouldBeEmpty(),
            Mounted = (_, binding, _, _) =>
                descendants = binding.GetDescendantElements("option"),
        };
        ElementNode root = new(
            new QualifiedName("select"),
            children:
            [
                new ElementNode(new QualifiedName("option")),
                new FragmentNode(
                [
                    new ElementNode(new QualifiedName("option")),
                ]),
            ],
            directives:
            [
                new DirectiveInvocation(typeof(RecordingDirectiveToken)),
            ]);

        renderer.Render(root, host.Container, CreateApplication(root, directive));
        host.RunScheduledFlushes();

        descendants.ShouldNotBeNull().Count.ShouldBe(2);
        descendants[0].Value.Name.LocalName.ShouldBe("option");
        descendants[1].Value.Name.LocalName.ShouldBe("option");
    }

    [Fact]
    public void Hydrate_Directive_AdoptsElementAndAttachesLifecycle()
    {
        Scheduler.Reset();
        Queue<Action> scheduledFlushes = [];
        using IDisposable registration = Scheduler.UseFlushDispatcher(
            scheduledFlushes.Enqueue);
        try
        {
            HydrationWalkerFakeHost host = new();
            HydrationWalkerHostNode serverElement = host.CreateServerElement("section");
            host.AppendServerChild(host.Root, serverElement);
            List<string> phases = [];
            var directive = new Directive
            {
                Created = (_, _, _, _) => phases.Add("created"),
                BeforeMount = (_, _, _, _) => phases.Add("before-mount"),
                Mounted = (_, _, _, _) => phases.Add("mounted"),
            };
            ElementNode root = new(
                new QualifiedName("section"),
                directives:
                [
                    new DirectiveInvocation(typeof(RecordingDirectiveToken)),
                ]);
            Renderer<HydrationWalkerHostNode> renderer =
                RendererFactory.CreateRenderer(host.Options);

            renderer.Hydrate(root, host.Root, CreateApplication(root, directive));
            while (scheduledFlushes.Count > 0)
            {
                scheduledFlushes.Dequeue()();
            }

            host.Root.Children.ShouldHaveSingleItem().ShouldBeSameAs(serverElement);
            host.ClientCreationCount.ShouldBe(0);
            phases.ShouldBe(["created", "before-mount", "mounted"]);
        }
        finally
        {
            Scheduler.Reset();
        }
    }

    private static ElementNode DirectedElement(int value) =>
        new(
            new QualifiedName("root"),
            directives:
            [
                new DirectiveInvocation(typeof(RecordingDirectiveToken), value),
            ]);

    private static ElementNode LifecycleElement(string value, List<string> phases)
    {
        ElementBinding Hook(string name, string phase) =>
            ElementBinding.Property(
                name,
                new VirtualNodeLifecycleHook(
                    (current, previous) => phases.Add(
                        $"{phase}:{Text(current)}:{Text(previous)}")));

        return new ElementNode(
            new QualifiedName("root"),
            bindings:
            [
                Hook("onVnodeBeforeMount", "before-mount"),
                Hook("onVnodeMounted", "mounted"),
                Hook("onVnodeBeforeUpdate", "before-update"),
                Hook("onVnodeUpdated", "updated"),
                Hook("onVnodeBeforeUnmount", "before-unmount"),
                Hook("onVnodeUnmounted", "unmounted"),
            ],
            children: [new TextNode(value)]);
    }

    private static string Text(VirtualNode? value) =>
        value is ElementNode { Children.Count: > 0 } element
            && element.Children[0] is TextNode text
                ? text.Text
                : "null";

    private static Directive RecordingDirective(List<string> phases) =>
        new()
        {
            Created = (_, binding, _, _) => phases.Add(Describe("created", binding)),
            BeforeMount = (_, binding, _, _) =>
                phases.Add(Describe("before-mount", binding)),
            Mounted = (_, binding, _, _) => phases.Add(Describe("mounted", binding)),
            BeforeUpdate = (_, binding, _, _) =>
                phases.Add(Describe("before-update", binding)),
            Updated = (_, binding, _, _) => phases.Add(Describe("updated", binding)),
            BeforeUnmount = (_, binding, _, _) =>
                phases.Add(Describe("before-unmount", binding)),
            Unmounted = (_, binding, _, _) => phases.Add(Describe("unmounted", binding)),
        };

    private static string Describe(string phase, DirectiveBinding binding) =>
        $"{phase}:{binding.Value}:{binding.PreviousValue ?? "null"}";

    private static ApplicationContext CreateApplication(
        VirtualNode root,
        IDirective directive,
        Action<Exception, ComponentContext?, string>? errorHandler = null) =>
        new(
            new ApplicationOptions
            {
                RootComponent = root,
                Directives = new DirectiveRegistry(
                [
                    new KeyValuePair<Type, IDirective>(
                        typeof(RecordingDirectiveToken),
                        directive),
                ]),
                ErrorHandler = errorHandler,
            });

    private sealed class RecordingDirectiveToken
    {
    }
}
