using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins Core's host-neutral hydration adoption and smallest-range fallback semantics.
/// </summary>
/// <remarks>
/// Specified by <c>[HYD-1]</c> through <c>[HYD-7]</c> and <c>[BLT-5]</c> through
/// <c>[BLT-6]</c>.
/// </remarks>
public sealed class HydrationWalkerAdoptionTests
{
    [Fact]
    public void Hydrate_ElementTextAndComment_AdoptsNodesAndPatchesThemLater()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverText = host.CreateServerText("ready");
        HydrationWalkerHostNode serverComment = host.CreateServerComment("note");
        HydrationWalkerHostNode serverElement = host.CreateServerElement(
            "section",
            serverText,
            serverComment);
        serverElement.Attributes["id"] = "content";
        host.AppendServerChild(host.Root, serverElement);
        Action listener = () => { };
        ElementNode client = new(
            new QualifiedName("section"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("id"), "content"),
                ElementBinding.Event("click", listener),
            ],
            children:
            [
                new TextNode("ready"),
                new CommentNode("note"),
            ]);
        List<string> warnings = [];
        ApplicationContext application = CreateApplication(client, warnings);
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, application);

        host.Root.Children.ShouldBe([serverElement]);
        serverElement.Children.ShouldBe([serverText, serverComment]);
        host.ClientCreationCount.ShouldBe(0);
        host.Operations.ShouldContain(
            $"binding:{serverElement.Identifier}:Event:click");
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("remove:", StringComparison.Ordinal));
        warnings.ShouldBeEmpty();

        ElementNode updated = new(
            new QualifiedName("section"),
            children:
            [
                new TextNode("updated"),
                new CommentNode("note"),
            ]);
        renderer.Render(updated, host.Root, application);

        host.Root.Children.Single().ShouldBeSameAs(serverElement);
        serverElement.Children[0].ShouldBeSameAs(serverText);
        serverText.Data.ShouldBe("updated");
    }

    [Fact]
    public void Hydrate_TextMismatch_CorrectsTheAdoptedNodeWithoutReplacement()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverText = host.CreateServerText("server");
        host.AppendServerChild(host.Root, serverText);
        TextNode client = new("client");
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, CreateApplication(client, warnings));

        host.Root.Children.Single().ShouldBeSameAs(serverText);
        serverText.Data.ShouldBe("client");
        host.ClientCreationCount.ShouldBe(0);
        warnings.ShouldHaveSingleItem()
            .ShouldContain("Hydration text mismatch");
    }

    [Fact]
    public void Hydrate_FragmentWithStaticAndComment_AdoptsTheMarkerBoundedRange()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode start = host.CreateServerComment(
            CommentData(HydrationMarkers.FragmentStart));
        HydrationWalkerHostNode staticElement = host.CreateServerElement("em");
        HydrationWalkerHostNode comment = host.CreateServerComment("tail");
        HydrationWalkerHostNode end = host.CreateServerComment(
            CommentData(HydrationMarkers.FragmentEnd));
        host.AppendServerChild(host.Root, start);
        host.AppendServerChild(host.Root, staticElement);
        host.AppendServerChild(host.Root, comment);
        host.AppendServerChild(host.Root, end);
        FragmentNode client = new(
        [
            new StaticNode(MarkupFormat.Html, "<em></em>"),
            new CommentNode("tail"),
        ]);
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([start, staticElement, comment, end]);
        host.ClientCreationCount.ShouldBe(0);
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("remove:", StringComparison.Ordinal));
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_MismatchedFragmentRange_ReplacesOnlyThatCompleteRange()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode start = host.CreateServerComment(
            CommentData(HydrationMarkers.FragmentStart));
        HydrationWalkerHostNode stale = host.CreateServerElement("span");
        HydrationWalkerHostNode end = host.CreateServerComment(
            CommentData(HydrationMarkers.FragmentEnd));
        HydrationWalkerHostNode retained = host.CreateServerElement("p");
        HydrationWalkerHostNode serverRoot = host.CreateServerElement(
            "div",
            start,
            stale,
            end,
            retained);
        host.AppendServerChild(host.Root, serverRoot);
        ElementNode client = new(
            new QualifiedName("div"),
            children:
            [
                new ElementNode(new QualifiedName("article")),
                new ElementNode(new QualifiedName("p")),
            ]);
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, CreateApplication(client, warnings));

        host.Root.Children.Single().ShouldBeSameAs(serverRoot);
        serverRoot.Children.Count.ShouldBe(2);
        serverRoot.Children[0].Data.ShouldBe("article");
        serverRoot.Children[1].ShouldBeSameAs(retained);
        start.Parent.ShouldBeNull();
        stale.Parent.ShouldBeNull();
        end.Parent.ShouldBeNull();
        host.ClientCreationCount.ShouldBe(1);
        host.Operations.Count(
                operation => operation.StartsWith("remove:", StringComparison.Ordinal))
            .ShouldBe(3);
        warnings.ShouldHaveSingleItem()
            .ShouldContain("Hydration node mismatch");
    }

    [Fact]
    public void Hydrate_TrailingRootNodes_RemovesOnlyTheLeftovers()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode retained = host.CreateServerElement("main");
        HydrationWalkerHostNode excessElement = host.CreateServerElement("aside");
        HydrationWalkerHostNode excessText = host.CreateServerText("tail");
        host.AppendServerChild(host.Root, retained);
        host.AppendServerChild(host.Root, excessElement);
        host.AppendServerChild(host.Root, excessText);
        ElementNode client = new(new QualifiedName("main"));
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([retained]);
        excessElement.Parent.ShouldBeNull();
        excessText.Parent.ShouldBeNull();
        host.ClientCreationCount.ShouldBe(0);
        warnings.ShouldHaveSingleItem()
            .ShouldContain("extra root nodes");
    }

    [Fact]
    public void Hydrate_ComponentSubtree_AdoptsThenUpdatesTheExistingHostNodes()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverText = host.CreateServerText("ready");
        HydrationWalkerHostNode serverElement = host.CreateServerElement("strong", serverText);
        host.AppendServerChild(host.Root, serverElement);
        ComponentReference reference = ComponentReference.ForType(
            typeof(HydrationWalkerGreetingComponent));
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(
                    parameters: [new ComponentParameter("message")]),
                _ => new HydrationWalkerGreetingComponent()));
        ComponentNode client = new(
            reference,
            new ComponentInvocation(
                arguments: new Dictionary<string, object?> { ["message"] = "ready" }));
        List<string> warnings = [];
        ApplicationContext application = CreateApplication(client, warnings, components);
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        ComponentContext? context = renderer.Hydrate(client, host.Root, application);

        context.ShouldNotBeNull();
        host.Root.Children.Single().ShouldBeSameAs(serverElement);
        serverElement.Children.Single().ShouldBeSameAs(serverText);
        host.ClientCreationCount.ShouldBe(0);
        renderer.GetMountedComponentViews(host.Root)
            .ShouldHaveSingleItem()
            .Instance.ShouldBeOfType<HydrationWalkerGreetingComponent>();

        ComponentNode updated = new(
            reference,
            new ComponentInvocation(
                arguments: new Dictionary<string, object?> { ["message"] = "updated" }));
        renderer.Render(updated, host.Root, application);

        host.Root.Children.Single().ShouldBeSameAs(serverElement);
        serverElement.Children.Single().ShouldBeSameAs(serverText);
        serverText.Data.ShouldBe("updated");
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_EnabledTeleport_AdoptsOriginAndTargetMarkerRanges()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode originStart = host.CreateServerComment(
            CommentData(HydrationMarkers.TeleportStart));
        HydrationWalkerHostNode originEnd = host.CreateServerComment(
            CommentData(HydrationMarkers.TeleportEnd));
        host.AppendServerChild(host.Root, originStart);
        host.AppendServerChild(host.Root, originEnd);
        HydrationWalkerHostNode target = host.CreateServerElement("target");
        HydrationWalkerHostNode targetText = host.CreateServerText("away");
        HydrationWalkerHostNode targetAnchor = host.CreateServerComment(
            CommentData(HydrationMarkers.TeleportAnchor));
        host.AppendServerChild(target, targetText);
        host.AppendServerChild(target, targetAnchor);
        host.RegisterTarget("destination", target);
        TeleportNode client = new(
            "destination",
            children: [new TextNode("away")]);
        List<string> warnings = [];
        ApplicationContext application = CreateApplication(client, warnings);
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, application);

        host.Root.Children.ShouldBe([originStart, originEnd]);
        target.Children.ShouldBe([targetText, targetAnchor]);
        host.ClientCreationCount.ShouldBe(0);
        warnings.ShouldBeEmpty();

        TeleportNode updated = new(
            "destination",
            children: [new TextNode("returned")]);
        renderer.Render(updated, host.Root, application);

        target.Children[0].ShouldBeSameAs(targetText);
        targetText.Data.ShouldBe("returned");
    }

    [Fact]
    public void Hydrate_UnresolvedTeleportTarget_WarnsAndAdoptsTheOriginRange()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode originStart = host.CreateServerComment(
            CommentData(HydrationMarkers.TeleportStart));
        HydrationWalkerHostNode originEnd = host.CreateServerComment(
            CommentData(HydrationMarkers.TeleportEnd));
        host.AppendServerChild(host.Root, originStart);
        host.AppendServerChild(host.Root, originEnd);
        TeleportNode client = new(
            "missing",
            children: [new TextNode("unavailable")]);
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([originStart, originEnd]);
        host.ClientCreationCount.ShouldBe(0);
        warnings.ShouldHaveSingleItem()
            .ShouldBe("Failed to resolve teleport target 'missing'.");
    }

    [Fact]
    public void Hydrate_KeepAlive_AdoptsMatchingComponentSubtreeAndRetainsItForUpdates()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverText = host.CreateServerText("ready");
        HydrationWalkerHostNode serverElement = host.CreateServerElement("strong", serverText);
        host.AppendServerChild(host.Root, serverElement);
        ComponentReference reference = ComponentReference.ForType(
            typeof(HydrationWalkerGreetingComponent));
        ComponentFactory components = new();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(
                    parameters: [new ComponentParameter("message")]),
                _ => new HydrationWalkerGreetingComponent()));
        KeepAliveNode client = KeepAliveGreeting(reference, "ready");
        List<string> warnings = [];
        ApplicationContext application = CreateApplication(client, warnings, components);
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, application);

        host.Root.Children.Count.ShouldBe(3);
        host.Root.Children[0].Kind.ShouldBe(HydrationNodeKind.Comment);
        host.Root.Children[0].Data.ShouldBe("keep-alive start");
        host.Root.Children[1].ShouldBeSameAs(serverElement);
        host.Root.Children[2].Kind.ShouldBe(HydrationNodeKind.Comment);
        host.Root.Children[2].Data.ShouldBe("keep-alive end");
        serverElement.Children.Single().ShouldBeSameAs(serverText);
        host.ClientCreationCount.ShouldBe(3);
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("remove:", StringComparison.Ordinal));
        warnings.ShouldBeEmpty();
        renderer.GetMountedComponentViews(host.Root)
            .ShouldHaveSingleItem()
            .FirstHostNode.ShouldBeSameAs(serverElement);

        KeepAliveNode updated = KeepAliveGreeting(reference, "updated");
        renderer.Render(updated, host.Root, application);

        host.Root.Children[1].ShouldBeSameAs(serverElement);
        serverElement.Children.Single().ShouldBeSameAs(serverText);
        serverText.Data.ShouldBe("updated");
        host.ClientCreationCount.ShouldBe(3);
        warnings.ShouldBeEmpty();
    }

    private static KeepAliveNode KeepAliveGreeting(
        ComponentReference reference,
        string message)
    {
        return new KeepAliveNode(
            new ComponentInvocation(
                slots: new Dictionary<string, ComponentSlot>(StringComparer.Ordinal)
                {
                    ["default"] = _ => new ComponentNode(
                        reference,
                        new ComponentInvocation(
                            arguments: new Dictionary<string, object?>
                            {
                                ["message"] = message,
                            }),
                        key: "greeting"),
                }));
    }

    private static ApplicationContext CreateApplication(
        VirtualNode root,
        List<string> warnings,
        IComponentFactory? components = null) =>
        new(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components ?? new ComponentFactory(),
                WarnHandler = warnings.Add,
            });

    private static string CommentData(string serializedComment) =>
        serializedComment[4..^3];

    private sealed class HydrationWalkerGreetingComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new ElementNode(
                new QualifiedName("strong"),
                children:
                [
                    new TextNode(
                        (string)context.Bindings.Parameters["message"]!),
                ]);
    }
}
