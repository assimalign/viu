using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class RendererBlockTeardownTests
{
    [Fact]
    public void Render_StaticBlockWithNestedComponent_FullyUnmountsSkippedComponent()
    {
        VerifyNestedComponentTeardown(collectComponent: false);
    }

    [Fact]
    public void Render_BlockWithCollectedNestedComponent_DoesNotUnmountComponentTwice()
    {
        VerifyNestedComponentTeardown(collectComponent: true);
    }

    private static void VerifyNestedComponentTeardown(bool collectComponent)
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        int unmountedCount = 0;
        ComponentReference reference = ComponentReference.ForType(
            typeof(BlockTeardownComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new BlockTeardownComponent(() => unmountedCount++)));
        var component = new ComponentNode(reference);
        var wrapper = new ElementNode(
            new QualifiedName("wrapper"),
            children: [component]);
        IReadOnlyList<VirtualNode> dynamicChildren = collectComponent
            ? [component]
            : Array.Empty<VirtualNode>();
        var root = new ElementNode(
            new QualifiedName("root"),
            children: [wrapper],
            renderPlan: new RenderPlan(
                PatchFlags.NeedPatch,
                dynamicChildren: dynamicChildren));
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
            });
        renderer.Render(root, host.Container, application);
        host.RunScheduledFlushes();
        Renderer<RendererParityNode>.UnmountVisitCount = 0;

        renderer.Render(null, host.Container);

        unmountedCount.ShouldBe(1);
        Renderer<RendererParityNode>.UnmountVisitCount.ShouldBe(3);
        renderer.GetMountedComponentViews(host.Container).ShouldBeEmpty();
    }

    private sealed class BlockTeardownComponent : IComponent
    {
        private readonly Action _onUnmounted;

        internal BlockTeardownComponent(Action onUnmounted)
        {
            _onUnmounted = onUnmounted;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Lifecycle.OnUnmounted(_onUnmounted);
            return static _ => new TextNode("component");
        }
    }
}
