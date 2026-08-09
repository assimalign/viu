using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerRendererContextAndStreamingTests
{
    [Fact]
    public async Task Teleport_EnabledAndDisabled_UseOriginAndTargetMarkerProtocol()
    {
        VirtualNode root = Element(
            "main",
            children:
            [
                new TeleportNode(
                    "#modal",
                    [Element("p", children: [new TextNode("enabled")])]),
                new TeleportNode(
                    "#modal",
                    [Element("p", children: [new TextNode("disabled")])],
                    isDisabled: true),
            ]);
        SsrContext context = new();

        string html = await ServerRenderer.RenderToStringAsync(root, context);

        html.ShouldBe(
            "<main>"
            + HydrationMarkers.TeleportStart
            + HydrationMarkers.TeleportEnd
            + HydrationMarkers.TeleportStart
            + "<p>disabled</p>"
            + HydrationMarkers.TeleportEnd
            + "</main>");
        context.Teleports["#modal"].ShouldBe(
            "<p>enabled</p>"
            + HydrationMarkers.TeleportAnchor
            + HydrationMarkers.TeleportAnchor);
    }

    [Fact]
    public async Task Teleport_MultipleEnabledContributions_AccumulateInTreeOrder()
    {
        VirtualNode root = new FragmentNode(
        [
            new TeleportNode("#target", [new TextNode("first")]),
            new TeleportNode("#target", [new TextNode("second")]),
        ]);
        SsrContext context = new();

        await ServerRenderer.RenderToStringAsync(root, context);

        context.Teleports["#target"].ShouldBe(
            "first"
            + HydrationMarkers.TeleportAnchor
            + "second"
            + HydrationMarkers.TeleportAnchor);
    }

    [Fact]
    public async Task SsrContext_NoComposedStateRegistry_LeavesPayloadAbsent()
    {
        SsrContext context = new();

        string html = await ServerRenderer.RenderToStringAsync(new TextNode("content"), context);

        html.ShouldBe("content");
        context.State.ShouldBeNull();
        context.Teleports.ShouldBeEmpty();
    }

    [Fact]
    public async Task RenderToStreamAsync_PrimitiveTree_MatchesStringRendering()
    {
        VirtualNode root = Element(
            "div",
            bindings: [ElementBinding.Attribute(new QualifiedName("id"), "app")],
            children: [new TextNode("hello")]);
        using StringWriter writer = new();

        await ServerRender.RenderToStreamAsync(root, writer);

        writer.ToString().ShouldBe(await ServerRender.RenderToStringAsync(root));
    }

    [Fact]
    public async Task RenderToStreamAsync_NestedComponents_FlushesEveryCompletedSubtree()
    {
        ComponentReference parentReference = ComponentReference.ForName("parent");
        ComponentReference firstReference = ComponentReference.ForName("first");
        ComponentReference secondReference = ComponentReference.ForName("second");
        ComponentFactory components = new();
        components.Register(Registration(
            parentReference,
            _ => Element(
                "div",
                children:
                [
                    new ComponentNode(firstReference),
                    new ComponentNode(secondReference),
                ])));
        components.Register(Registration(
            firstReference,
            _ => Element("span", children: [new TextNode("a")])));
        components.Register(Registration(
            secondReference,
            _ => Element("span", children: [new TextNode("b")])));
        RecordingTextWriter writer = new();

        await ServerRenderer.RenderToStreamAsync(
            new ServerRenderApplication(new ComponentNode(parentReference), components),
            writer);

        writer.Text.ShouldBe("<div><span>a</span><span>b</span></div>");
        writer.Chunks.ShouldBe(
        [
            "<div><span>a</span>",
            "<span>b</span>",
            "</div>",
        ]);
        writer.FlushCount.ShouldBe(3);
    }

    [Fact]
    public async Task ServerApplicationBuilder_SnapshotsServicesAndDoesNotDisposeThem()
    {
        ComponentReference reference = ComponentReference.ForName("service");
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new InlineComponent(context => _ =>
                {
                    string value = (string?)context.Services?.GetService(typeof(string))
                        ?? "missing";
                    return new TextNode(value);
                })));
        TrackingServiceProvider first = new("first");
        TrackingServiceProvider second = new("second");
        ServerApplicationBuilder builder = ServerRenderApplication.CreateBuilder(
            new ComponentNode(reference),
            components,
            first);
        ServerRenderApplication application = builder.Build();
        builder.ConfigureApplication(options => options.Services = second);

        string html = await ServerRenderer.RenderToStringAsync(application);

        html.ShouldBe("first");
        first.IsDisposed.ShouldBeFalse();
        second.IsDisposed.ShouldBeFalse();
    }

    private static ComponentRegistration Registration(
        ComponentReference reference,
        Func<ComponentContext, VirtualNode?> render) =>
        new(
            reference,
            new ComponentContract(),
            _ => new InlineComponent(context => _ => render(context)));

    private static ElementNode Element(
        string name,
        IReadOnlyList<ElementBinding>? bindings = null,
        IReadOnlyList<VirtualNode>? children = null) =>
        new(new QualifiedName(name), bindings, children);

    private sealed class InlineComponent : IComponent
    {
        private readonly Func<ComponentContext, ComponentRenderer> _setup;

        internal InlineComponent(Func<ComponentContext, ComponentRenderer> setup)
        {
            _setup = setup;
        }

        public ComponentRenderer Setup(ComponentContext context) => _setup(context);
    }

    private sealed class TrackingServiceProvider : IServiceProvider, IDisposable
    {
        private readonly string _value;

        internal TrackingServiceProvider(string value)
        {
            _value = value;
        }

        internal bool IsDisposed { get; private set; }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(string) ? _value : null;

        public void Dispose() => IsDisposed = true;
    }

    private sealed class RecordingTextWriter : TextWriter
    {
        private readonly StringBuilder _all = new();

        internal List<string> Chunks { get; } = [];

        internal int FlushCount { get; private set; }

        internal string Text => _all.ToString();

        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteAsync(
            ReadOnlyMemory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            string chunk = buffer.ToString();
            Chunks.Add(chunk);
            _all.Append(chunk);
            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCount++;
            return Task.CompletedTask;
        }
    }
}
