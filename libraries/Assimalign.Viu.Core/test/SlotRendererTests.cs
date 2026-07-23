using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;
using Assimalign.Viu.Tests;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins Vue 3.5's slot-stability portion of <c>shouldUpdateComponent</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/componentRenderUtils.ts.
/// </summary>
public sealed class SlotRendererTests : IDisposable
{
    private readonly TestSchedulerPump _pump;

    public SlotRendererTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
    }

    public void Dispose()
    {
        _pump.RunUntilIdle();
        _pump.Dispose();
        Scheduler.Reset();
    }

    [Fact]
    public void Render_StableSlotsWithUnchangedArguments_SkipsChildRender()
    {
        SlotTemplate template = new();
        ITemplateComponent initial = Request(template, SlotFlags.Stable, "content");
        IApplicationContext application = Application(initial, template);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(initial, host.Root, application);
        renderer.Render(
            Request(template, SlotFlags.Stable, "content"),
            host.Root);

        template.RenderCount.ShouldBe(1);
        host.Text(host.Root).ShouldBe("content");
    }

    [Fact]
    public void Render_DynamicSlots_ForceChildRender()
    {
        SlotTemplate template = new();
        ITemplateComponent initial = Request(template, SlotFlags.Dynamic, "before");
        IApplicationContext application = Application(initial, template);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(initial, host.Root, application);
        renderer.Render(
            Request(template, SlotFlags.Dynamic, "after"),
            host.Root);

        template.RenderCount.ShouldBe(2);
        host.Text(host.Root).ShouldBe("after");
    }

    [Fact]
    public void Render_DynamicSlotsPatchFlag_OverridesStableMarker()
    {
        SlotTemplate template = new();
        ITemplateComponent initial = Request(template, SlotFlags.Stable, "before");
        IApplicationContext application = Application(initial, template);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(initial, host.Root, application);
        renderer.Render(
            Request(
                template,
                SlotFlags.Stable,
                "after",
                new ComponentOptimization(PatchFlags.DynamicSlots)),
            host.Root);

        template.RenderCount.ShouldBe(2);
        host.Text(host.Root).ShouldBe("after");
    }

    [Fact]
    public void Render_ForwardedSlotsFromStableParent_DoNotForceGrandchildRender()
    {
        RunForwardingScenario(SlotFlags.Stable).ShouldBe(1);
    }

    [Fact]
    public void Render_ForwardedSlotsFromDynamicParent_ForceGrandchildRender()
    {
        RunForwardingScenario(SlotFlags.Dynamic).ShouldBe(2);
    }

    [Fact]
    public void Render_SlotReactiveRead_AttributesToInvokingChildNotDefiningParent()
    {
        Reference<string> slotDependency = Reactive.Reference("before");
        ReactiveSlotParentTemplate parent =
            new(slotDependency);
        ReactiveSlotChildTemplate child = new();
        ITemplateComponent root =
            ComponentTree.Template<ReactiveSlotParentTemplate>();
        IApplicationContext application =
            Application(root, parent, child);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        parent.RenderCount.ShouldBe(1);
        child.RenderCount.ShouldBe(1);
        host.Text(host.Root).ShouldBe("before");

        slotDependency.Value = "after";
        _pump.RunUntilIdle();

        parent.RenderCount.ShouldBe(1);
        child.RenderCount.ShouldBe(2);
        host.Text(host.Root).ShouldBe("after");
    }

    private static ITemplateComponent Request(
        SlotTemplate template,
        SlotFlags flags,
        string content,
        ComponentOptimization? optimization = null)
    {
        ComponentSlots slots = new(flags)
        {
            ["default"] = _ => ComponentTree.Text(content),
        };
        return ComponentTree.Template(
            template.GetType(),
            slots: slots,
            optimization: optimization);
    }

    private static IApplicationContext Application(
        IComponent root,
        params IComponentTemplate[] templates)
    {
        ComponentRegistration[] registrations =
            new ComponentRegistration[templates.Length];
        for (int index = 0; index < templates.Length; index++)
        {
            IComponentTemplate template = templates[index];
            registrations[index] = new ComponentRegistration(
                template.GetType(),
                () => template);
        }

        return new ApplicationContext(
            root,
            new ComponentFactory(registrations),
            new EmptyServiceProvider());
    }

    private int RunForwardingScenario(SlotFlags parentFlags)
    {
        Reference<string> parentState = Reactive.Reference("before");
        ForwardingParentTemplate parent =
            new(parentState, parentFlags);
        ForwardingMiddleTemplate middle = new();
        ForwardingGrandchildTemplate grandchild = new();
        ITemplateComponent root =
            ComponentTree.Template<ForwardingParentTemplate>();
        IApplicationContext application =
            Application(root, parent, middle, grandchild);
        FakeHost host = new();
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Render(root, host.Root, application);
        grandchild.RenderCount.ShouldBe(1);
        host.Text(host.Root).ShouldBe("beforeleaf");

        parentState.Value = "after";
        _pump.RunUntilIdle();

        host.Text(host.Root).ShouldBe("afterleaf");
        return grandchild.RenderCount;
    }

    private sealed class SlotTemplate : IComponentTemplate
    {
        internal int RenderCount { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                RenderCount++;
                return RenderHelpers._renderSlot(context.Slots, "default");
            };
        }
    }

    private sealed class ForwardingParentTemplate : IComponentTemplate
    {
        private readonly Reference<string> _state;
        private readonly SlotFlags _slotFlags;

        internal ForwardingParentTemplate(
            Reference<string> state,
            SlotFlags slotFlags)
        {
            _state = state;
            _slotFlags = slotFlags;
        }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                ComponentSlots slots = new(_slotFlags)
                {
                    ["default"] = _ => ComponentTree.Text("leaf"),
                };
                return ComponentTree.Element(
                    "section",
                    children:
                    [
                        ComponentTree.Text(_state.Value),
                        ComponentTree.Template<ForwardingMiddleTemplate>(
                            slots: slots),
                    ]);
            };
        }
    }

    private sealed class ForwardingMiddleTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                ComponentSlots forwarded =
                    new(SlotFlags.Forwarded);
                if (context.Slots.TryGetValue(
                        "default",
                        out ComponentSlot? slot))
                {
                    forwarded["default"] =
                        arguments => slot(arguments);
                }

                return ComponentTree.Template<ForwardingGrandchildTemplate>(
                    slots: forwarded);
            };
        }
    }

    private sealed class ForwardingGrandchildTemplate : IComponentTemplate
    {
        internal int RenderCount { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                RenderCount++;
                return ComponentTree.Element(
                    "div",
                    children:
                    [
                        RenderHelpers._renderSlot(
                            context.Slots,
                            "default"),
                    ]);
            };
        }
    }

    private sealed class ReactiveSlotParentTemplate : IComponentTemplate
    {
        private readonly Reference<string> _slotDependency;

        internal ReactiveSlotParentTemplate(
            Reference<string> slotDependency)
        {
            _slotDependency = slotDependency;
        }

        internal int RenderCount { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                RenderCount++;
                ComponentSlots slots = new(SlotFlags.Stable)
                {
                    ["default"] = _ =>
                        ComponentTree.Text(_slotDependency.Value),
                };
                return ComponentTree.Template<ReactiveSlotChildTemplate>(
                    slots: slots);
            };
        }
    }

    private sealed class ReactiveSlotChildTemplate : IComponentTemplate
    {
        internal int RenderCount { get; private set; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            return () =>
            {
                RenderCount++;
                return ComponentTree.Element(
                    "div",
                    children:
                    [
                        RenderHelpers._renderSlot(
                            context.Slots,
                            "default"),
                    ]);
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
