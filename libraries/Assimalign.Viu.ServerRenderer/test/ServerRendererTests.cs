using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerRendererTests
{
    [Fact]
    public async Task RenderToStringAsync_ClosedAlgebra_SerializesAllTenNodeKinds()
    {
        ComponentReference componentReference = ComponentReference.ForName("component");
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                componentReference,
                new ComponentContract(),
                _ => new InlineComponent(
                    _ => _ => Element("strong", children: [new TextNode("component")]))));
        ComponentInvocation keepAliveInvocation = Invocation(
            ("default", _ => Element("i", children: [new TextNode("keep")])));
        ComponentInvocation suspenseInvocation = new(
            slots: new Dictionary<string, ComponentSlot>
            {
                ["default"] = _ => Element("b", children: [new TextNode("resolved")]),
                ["fallback"] = _ => Element("b", children: [new TextNode("fallback")]),
            });
        ComponentInvocation transitionInvocation = Invocation(
            ("default", _ => Element("u", children: [new TextNode("transition")])));
        VirtualNode root = new FragmentNode(
        [
            Element("div", children: [new TextNode("element child")]),
            new TextNode("<text>"),
            new CommentNode("comment"),
            new StaticNode(MarkupFormat.Html, "<em>static</em>"),
            new ComponentNode(componentReference),
            new TeleportNode("#target", [Element("span", children: [new TextNode("away")])]),
            new KeepAliveNode(keepAliveInvocation),
            new SuspenseNode(suspenseInvocation),
            new TransitionNode(transitionInvocation),
        ]);
        SsrContext context = new();
        ServerRenderApplication application = new(root, components);

        string html = await ServerRender.RenderToStringAsync(application, context);

        html.ShouldBe(
            HydrationMarkers.FragmentStart
            + "<div>element child</div>"
            + "&lt;text&gt;"
            + "<!--comment-->"
            + "<em>static</em>"
            + "<strong>component</strong>"
            + HydrationMarkers.TeleportStart
            + HydrationMarkers.TeleportEnd
            + "<i>keep</i>"
            + "<b>resolved</b>"
            + "<u>transition</u>"
            + HydrationMarkers.FragmentEnd);
        context.Teleports["#target"].ShouldBe(
            "<span>away</span>" + HydrationMarkers.TeleportAnchor);
    }

    [Fact]
    public async Task RenderToStringAsync_NestedComponent_UsesActiveParentLeaseContext()
    {
        ComponentReference parentReference = ComponentReference.ForName("parent");
        ComponentReference childReference = ComponentReference.ForName("child");
        ComponentContext? parentContext = null;
        ComponentContext? observedParent = null;
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                parentReference,
                new ComponentContract(),
                _ => new InlineComponent(context =>
                {
                    parentContext = context;
                    return _ => Element("section", children: [new ComponentNode(childReference)]);
                })));
        components.Register(
            new ComponentRegistration(
                childReference,
                new ComponentContract(),
                _ => new InlineComponent(context =>
                {
                    observedParent = context.Parent;
                    return _ => Element("p", children: [new TextNode("nested")]);
                })));

        string html = await ServerRenderer.RenderToStringAsync(
            new ServerRenderApplication(new ComponentNode(parentReference), components));

        html.ShouldBe("<section><p>nested</p></section>");
        observedParent.ShouldBeSameAs(parentContext);
    }

    [Fact]
    public async Task RenderToStringAsync_DeferredHydrationStrategies_EmitStableMarkerBoundaries()
    {
        ComponentReference reference = ComponentReference.ForName("lazy-markers");
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new InlineComponent(
                    _ => _ => Element("button", children: [new TextNode("ready")]))));
        HydrationStrategy[] strategies =
        [
            HydrationStrategy.OnIdle(),
            HydrationStrategy.OnVisible(),
            HydrationStrategy.OnMediaQuery("(min-width: 1px)"),
            HydrationStrategy.OnInteraction("click"),
        ];

        foreach (HydrationStrategy strategy in strategies)
        {
            ComponentNode root = new(
                reference,
                new ComponentInvocation(hydrationStrategy: strategy));

            string html = await ServerRenderer.RenderToStringAsync(
                new ServerRenderApplication(root, components));

            html.ShouldBe(
                HydrationMarkers.GetLazyHydrationStart(strategy.Kind)
                + "<button>ready</button>"
                + HydrationMarkers.LazyHydrationEnd);
        }
    }

    [Fact]
    public async Task RenderToStringAsync_LazyAsynchronousDefinition_EmitsOneBoundary()
    {
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.Define<AsynchronousWrapperIdentity>(
                new AsynchronousComponentOptions
                {
                    Loader = _ => Task.FromResult(
                        AsynchronousComponentTarget.From<AsynchronousTargetIdentity>()),
                    HydrationStrategy = HydrationStrategy.OnIdle(),
                });
        ComponentFactory components = new();
        components.Register(definition.Registration);
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(AsynchronousTargetIdentity)),
                new ComponentContract(),
                _ => new InlineComponent(
                    _ => _ => Element("button", children: [new TextNode("ready")]))));

        string html = await ServerRenderer.RenderToStringAsync(
            new ServerRenderApplication(definition.CreateComponent(), components));

        html.ShouldBe(
            HydrationMarkers.GetLazyHydrationStart(HydrationStrategyKind.Idle)
            + "<button>ready</button>"
            + HydrationMarkers.LazyHydrationEnd);
    }

    [Fact]
    public async Task RenderToStringAsync_ServerPrefetch_CompletesBeforeSingleRender()
    {
        ComponentReference reference = ComponentReference.ForName("prefetch");
        List<string> order = [];
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new InlineComponent(context =>
                {
                    context.Lifecycle.OnServerPrefetch(async () =>
                    {
                        await Task.Yield();
                        order.Add("prefetch");
                    });
                    return _ =>
                    {
                        order.Add("render");
                        return new TextNode("ready");
                    };
                })));

        string html = await ServerRenderer.RenderToStringAsync(
            new ServerRenderApplication(new ComponentNode(reference), components));

        html.ShouldBe("ready");
        order.ShouldBe(["prefetch", "render"]);
    }

    [Fact]
    public async Task RenderToStringAsync_CancelledPrefetch_CancelsComponentLifetime()
    {
        ComponentReference reference = ComponentReference.ForName("cancel");
        TaskCompletionSource prefetchStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource lifetimeCancelled = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new InlineComponent(context =>
                {
                    context.Lifecycle.CancellationToken.Register(
                        () => lifetimeCancelled.TrySetResult());
                    context.Lifecycle.OnServerPrefetch(async cancellationToken =>
                    {
                        prefetchStarted.SetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    });
                    return _ => new TextNode("never");
                })));
        using CancellationTokenSource cancellationSource = new();

        Task<string> rendering = ServerRenderer.RenderToStringAsync(
            new ServerRenderApplication(new ComponentNode(reference), components),
            cancellationToken: cancellationSource.Token);
        await prefetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () => await rendering);
        await lifetimeCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RenderToStringAsync_HandledRenderFailure_IsByteIdenticalAcrossTraversalAndCompiledBodies()
    {
        ComponentReference reference = ComponentReference.ForName("failure");
        List<string> diagnostics = [];
        InvalidOperationException failure = new("failure");
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new InlineComponent(
                    _ => _ => throw failure)));
        ServerRenderRegistry serverRenders = new();
        serverRenders.Register(new ServerRenderRegistration(
            reference,
            async (state, _, _, _) =>
            {
                state.Push("<strong>discarded</strong>");
                await ServerRender.SsrRenderTeleportAsync(
                    state,
                    teleportState =>
                    {
                        teleportState.Push("<span>discarded</span>");
                        return Task.CompletedTask;
                    },
                    "#discarded",
                    disabled: false);
                await state.FlushAsync();
                throw failure;
            }));
        ComponentNode root = new(reference);
        SsrContext traversalContext = new();
        SsrContext compiledContext = new();

        ServerRenderApplication CreateApplication() => ServerRenderApplication
            .CreateBuilder(root, components)
            .ConfigureApplication(options =>
                options.ErrorHandler = (_, _, diagnosticInformation) =>
                    diagnostics.Add(diagnosticInformation))
            .Build();

        string traversed = await ServerRenderer.RenderToStringAsync(
            CreateApplication(),
            traversalContext);
        string compiled = await ServerRenderer.RenderToStringAsync(
            CreateApplication(),
            serverRenders,
            compiledContext);

        traversed.ShouldBe(HydrationMarkers.EmptyComment);
        compiled.ShouldBe(traversed);
        traversalContext.Teleports.ShouldBeEmpty();
        compiledContext.Teleports.ShouldBeEmpty();
        diagnostics.ShouldBe(["component render", "component render"]);
    }

    [Fact]
    public async Task RenderToStringAsync_UnhandledRenderFailure_HasSameExceptionAcrossTraversalAndCompiledBodies()
    {
        ComponentReference reference = ComponentReference.ForName("unhandled-failure");
        InvalidOperationException failure = new("unhandled failure");
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new InlineComponent(
                    _ => _ => throw failure)));
        ServerRenderRegistry serverRenders = new();
        serverRenders.Register(new ServerRenderRegistration(
            reference,
            (_, _, _, _) => Task.FromException(failure)));
        ComponentNode root = new(reference);

        InvalidOperationException traversed = await Should.ThrowAsync<InvalidOperationException>(
            async () => await ServerRenderer.RenderToStringAsync(
                new ServerRenderApplication(root, components)));
        InvalidOperationException compiled = await Should.ThrowAsync<InvalidOperationException>(
            async () => await ServerRenderer.RenderToStringAsync(
                new ServerRenderApplication(root, components),
                serverRenders));

        traversed.ShouldBeSameAs(failure);
        compiled.ShouldBeSameAs(traversed);
    }

    [Fact]
    public async Task RenderToStringAsync_ExtensibleMarkupLanguageStaticNode_IsRejected()
    {
        StaticNode node = new(MarkupFormat.ExtensibleMarkupLanguage, "<node />");

        NotSupportedException exception = await Should.ThrowAsync<NotSupportedException>(
            async () => await ServerRenderer.RenderToStringAsync(node));

        exception.Message.ShouldContain("non-HTML");
    }

    private static ElementNode Element(
        string name,
        IReadOnlyList<ElementBinding>? bindings = null,
        IReadOnlyList<VirtualNode>? children = null) =>
        new(new QualifiedName(name), bindings, children);

    private static ComponentInvocation Invocation(
        params (string Name, ComponentSlot Slot)[] slots)
    {
        Dictionary<string, ComponentSlot> map = new(StringComparer.Ordinal);
        foreach ((string name, ComponentSlot slot) in slots)
        {
            map.Add(name, slot);
        }

        return new ComponentInvocation(slots: map);
    }

    private sealed class InlineComponent : IComponent
    {
        private readonly Func<ComponentContext, ComponentRenderer> _setup;

        internal InlineComponent(Func<ComponentContext, ComponentRenderer> setup)
        {
            _setup = setup;
        }

        public ComponentRenderer Setup(ComponentContext context) => _setup(context);
    }

    private sealed class AsynchronousWrapperIdentity : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => static _ => null;
    }

    private sealed class AsynchronousTargetIdentity : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => static _ => null;
    }
}
