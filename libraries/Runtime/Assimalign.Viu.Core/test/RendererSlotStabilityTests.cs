using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class RendererSlotStabilityTests
{
    private static readonly IReadOnlyDictionary<string, object?> EmptySlotArguments =
        new Dictionary<string, object?>();

    [Fact]
    public void ComponentInvocation_OmittedSlotStability_DefaultsToStable()
    {
        var slots = new Dictionary<string, ComponentSlot>
        {
            ["default"] = static _ => new TextNode("content"),
        };

        new ComponentInvocation().SlotStability.ShouldBe(SlotStability.Stable);
        new ComponentInvocation(slots: slots).SlotStability.ShouldBe(SlotStability.Stable);
        new ComponentInvocation(slots: slots, slotStability: SlotStability.Dynamic)
            .SlotStability.ShouldBe(SlotStability.Dynamic);
    }

    [Fact]
    public void ComponentInvocation_InvalidSlotStability_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new ComponentInvocation(slotStability: (SlotStability)0));
    }

    [Fact]
    public void Render_StableSlotsWithUnchangedArguments_SkipsChildRenderAndUpdate()
    {
        SlotScenario result = RunDirectScenario(
            SlotStability.Stable,
            "before",
            "after");

        // [CMP-18] a structurally stable slot replacement alone does not update the child.
        result.Counter.RenderCount.ShouldBe(1);
        result.Counter.BeforeUpdateCount.ShouldBe(0);
        result.Text.ShouldBe("before");
    }

    [Fact]
    public void Render_DynamicSlots_ForceChildRenderAndUpdate()
    {
        SlotScenario result = RunDirectScenario(
            SlotStability.Dynamic,
            "before",
            "after");

        // [CMP-18] dynamic slot structure forces a render even with unchanged arguments.
        result.Counter.RenderCount.ShouldBe(2);
        result.Counter.BeforeUpdateCount.ShouldBe(1);
        result.Text.ShouldBe("after");
    }

    [Fact]
    public void Render_DynamicSlotsPatchFlag_OverridesStableClassification()
    {
        SlotScenario result = RunDirectScenario(
            SlotStability.Stable,
            "before",
            "after",
            new RenderPlan(PatchFlags.DynamicSlots));

        result.Counter.RenderCount.ShouldBe(2);
        result.Counter.BeforeUpdateCount.ShouldBe(1);
        result.Text.ShouldBe("after");
    }

    [Fact]
    public void Render_ForwardedSlotsFromStableParent_InheritStableSkip()
    {
        SlotScenario result = RunForwardedScenario(SlotStability.Stable);

        result.Counter.RenderCount.ShouldBe(1);
        result.Counter.BeforeUpdateCount.ShouldBe(0);
        result.Text.ShouldBe("before");
    }

    [Fact]
    public void Render_ForwardedSlotsFromDynamicParent_InheritDynamicForce()
    {
        SlotScenario result = RunForwardedScenario(SlotStability.Dynamic);

        result.Counter.RenderCount.ShouldBe(2);
        result.Counter.BeforeUpdateCount.ShouldBe(1);
        result.Text.ShouldBe("after");
    }

    [Fact]
    public void Render_ComponentRootInvocation_TransfersDirectivesAndLifecycleOnly()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        int directiveCreatedCount = 0;
        int lifecycleMountedCount = 0;
        ComponentReference reference = ComponentReference.ForName("root-transfer-component");
        var registration = new ComponentRegistration(
            reference,
            new ComponentContract(flags: ComponentFlags.None),
            _ => new ElementRootComponent());
        var components = new ComponentFactory();
        components.Register(registration);
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?>
            {
                ["title"] = "must not transfer",
                ["onVnodeMounted"] = new VirtualNodeLifecycleHook(
                    (_, _) => lifecycleMountedCount++),
            },
            directives:
            [
                new DirectiveInvocation(typeof(RootTransferDirectiveToken)),
            ],
            slotStability: SlotStability.Stable);
        var request = new ComponentNode(reference, invocation);
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = request,
                Components = components,
                Directives = new DirectiveRegistry(
                [
                    new KeyValuePair<Type, IDirective>(
                        typeof(RootTransferDirectiveToken),
                        new Directive
                        {
                            Created = (_, _, _, _) => directiveCreatedCount++,
                        }),
                ]),
            });

        renderer.Render(request, host.Container, application);
        host.RunScheduledFlushes();

        directiveCreatedCount.ShouldBe(1);
        lifecycleMountedCount.ShouldBe(1);
        RendererParityNode element = host.Container.Children.ShouldHaveSingleItem();
        element.Bindings.ShouldNotContainKey("title");
        element.Bindings.ShouldNotContainKey("onVnodeMounted");
    }

    private static SlotScenario RunDirectScenario(
        SlotStability slotStability,
        string initialText,
        string nextText,
        RenderPlan? nextRenderPlan = null)
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        var counter = new SlotRunCounter();
        ComponentReference reference = ComponentReference.ForName("direct-slot-consumer");
        ComponentRegistration registration = CreateSlotConsumerRegistration(
            reference,
            counter);
        ComponentNode initial = CreateSlotRequest(reference, slotStability, initialText);
        ApplicationContext application = CreateApplication(initial, registration);

        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        renderer.Render(
            CreateSlotRequest(reference, slotStability, nextText, nextRenderPlan),
            host.Container);
        host.RunScheduledFlushes();

        return new SlotScenario(
            counter,
            host.Container.DescendantText);
    }

    private static SlotScenario RunForwardedScenario(SlotStability parentSlotStability)
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        var counter = new SlotRunCounter();
        ComponentReference childReference = ComponentReference.ForName(
            "forwarded-slot-consumer");
        ComponentReference parentReference = ComponentReference.ForName(
            "forwarding-slot-owner");
        ComponentRegistration childRegistration = CreateSlotConsumerRegistration(
            childReference,
            counter);
        var parentRegistration = new ComponentRegistration(
            parentReference,
            new ComponentContract(
                parameters: [new ComponentParameter("content")]),
            _ => new ForwardingSlotOwnerComponent(childReference));
        ComponentNode initial = CreateForwardingOwnerRequest(
            parentReference,
            parentSlotStability,
            "before");
        ApplicationContext application = CreateApplication(
            initial,
            childRegistration,
            parentRegistration);

        renderer.Render(initial, host.Container, application);
        host.RunScheduledFlushes();
        renderer.Render(
            CreateForwardingOwnerRequest(
                parentReference,
                parentSlotStability,
                "after"),
            host.Container);
        host.RunScheduledFlushes();

        return new SlotScenario(
            counter,
            host.Container.DescendantText);
    }

    private static ComponentRegistration CreateSlotConsumerRegistration(
        ComponentReference reference,
        SlotRunCounter counter) =>
        new(
            reference,
            new ComponentContract(),
            _ => new SlotConsumerComponent(counter));

    private static ComponentNode CreateSlotRequest(
        ComponentReference reference,
        SlotStability slotStability,
        string text,
        RenderPlan? renderPlan = null) =>
        new(
            reference,
            new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot>
                {
                    ["default"] = _ => new TextNode(text),
                },
                slotStability: slotStability),
            renderPlan: renderPlan);

    private static ComponentNode CreateForwardingOwnerRequest(
        ComponentReference reference,
        SlotStability slotStability,
        string content) =>
        new(
            reference,
            new ComponentInvocation(
                arguments: new Dictionary<string, object?>
                {
                    ["content"] = content,
                },
                slots: new Dictionary<string, ComponentSlot>
                {
                    ["owner"] = static _ => null,
                },
                slotStability: slotStability));

    private static ApplicationContext CreateApplication(
        ComponentNode root,
        params ComponentRegistration[] registrations)
    {
        var components = new ComponentFactory();
        for (int index = 0; index < registrations.Length; index++)
        {
            components.Register(registrations[index]);
        }

        return new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
            });
    }

    private sealed class SlotConsumerComponent : IComponent
    {
        private readonly SlotRunCounter _counter;

        internal SlotConsumerComponent(SlotRunCounter counter)
        {
            _counter = counter;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnBeforeUpdate(() => _counter.BeforeUpdateCount++);
            return _ =>
            {
                _counter.RenderCount++;
                return context.Bindings.Slots.TryGetValue(
                    "default",
                    out ComponentSlot? slot)
                        ? slot(EmptySlotArguments)
                        : new CommentNode("missing slot");
            };
        }
    }

    private sealed class ForwardingSlotOwnerComponent : IComponent
    {
        private readonly ComponentReference _childReference;

        internal ForwardingSlotOwnerComponent(ComponentReference childReference)
        {
            _childReference = childReference;
        }

        public ComponentRenderer Setup(ComponentContext context) =>
            _ =>
            {
                string content = (string)context.Bindings.Parameters["content"]!;
                return new ComponentNode(
                    _childReference,
                    new ComponentInvocation(
                        slots: new Dictionary<string, ComponentSlot>
                        {
                            ["default"] = _ => new TextNode(content),
                        },
                        slotStability: SlotStability.Forwarded));
            };
    }

    private sealed class ElementRootComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            static _ => new ElementNode(new QualifiedName("root-transfer"));
    }

    private sealed class RootTransferDirectiveToken
    {
    }

    private sealed class SlotRunCounter
    {
        internal int RenderCount { get; set; }

        internal int BeforeUpdateCount { get; set; }
    }

    private sealed record SlotScenario(SlotRunCounter Counter, string Text);
}
