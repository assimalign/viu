using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins the hydration-reader gates and semantic class/style mismatch comparison required by
/// [HYD-3] through [HYD-5].
/// </summary>
public sealed class HydrationMismatchParityTests
{
    [Fact]
    public void Hydrate_SemanticallyEquivalentClassAndStyle_AdoptsWithoutWarnings()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode server = host.CreateServerElement("article");
        server.Attributes["class"] = "selected card";
        server.Attributes["style"] = "padding: 1px; color: red";
        host.AppendServerChild(host.Root, server);
        ElementNode client = Element(
            "card selected",
            "color:red; padding:1px;");
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([server]);
        host.ClientCreationCount.ShouldBe(0);
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_ClassAndStyleMismatch_WarnsByCategoryWithoutReplacingElement()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode server = host.CreateServerElement("article");
        server.Attributes["class"] = "server";
        server.Attributes["style"] = "color: red";
        host.AppendServerChild(host.Root, server);
        ElementNode client = Element("client", "color: blue");
        List<string> warnings = [];
        Renderer<HydrationWalkerHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([server]);
        host.ClientCreationCount.ShouldBe(0);
        warnings.Count.ShouldBe(2);
        warnings.ShouldContain(
            warning => warning.Contains(
                "Hydration class mismatch",
                StringComparison.Ordinal));
        warnings.ShouldContain(
            warning => warning.Contains(
                "Hydration style mismatch",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Hydrate_AllowMismatchCategories_SuppressExpectedClassAndStyleWarnings()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode server = host.CreateServerElement("article");
        server.Attributes["class"] = "server";
        server.Attributes["style"] = "color: red";
        server.Attributes["data-allow-mismatch"] = "class, style";
        host.AppendServerChild(host.Root, server);
        ElementNode client = Element("client", "color: blue");
        List<string> warnings = [];

        RendererFactory.CreateRenderer(host.Options).Hydrate(
            client,
            host.Root,
            CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([server]);
        host.ClientCreationCount.ShouldBe(0);
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Hydrate_ElementTagMismatch_ReplacesOnlyMismatchedChildSubtree()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode serverChild = host.CreateServerElement(
            "span",
            host.CreateServerText("content"));
        HydrationWalkerHostNode serverRoot = host.CreateServerElement(
            "div",
            serverChild);
        host.AppendServerChild(host.Root, serverRoot);
        ElementNode client = new(
            new QualifiedName("div"),
            children:
            [
                new ElementNode(
                    new QualifiedName("p"),
                    children: [new TextNode("content")]),
            ]);
        List<string> warnings = [];

        RendererFactory.CreateRenderer(host.Options).Hydrate(
            client,
            host.Root,
            CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([serverRoot]);
        serverChild.Parent.ShouldBeNull();
        HydrationWalkerHostNode replacement = serverRoot.Children.ShouldHaveSingleItem();
        replacement.Data.ShouldBe("p");
        replacement.Children.ShouldHaveSingleItem().Data.ShouldBe("content");
        warnings.ShouldHaveSingleItem().ShouldContain("Hydration node mismatch");
    }

    [Fact]
    public void Hydrate_ExcessElementChildren_RemovesOnlyTrailingServerChildren()
    {
        HydrationWalkerFakeHost host = new();
        HydrationWalkerHostNode retained = host.CreateServerElement(
            "li",
            host.CreateServerText("retained"));
        HydrationWalkerHostNode excess = host.CreateServerElement(
            "li",
            host.CreateServerText("excess"));
        HydrationWalkerHostNode serverList = host.CreateServerElement(
            "ul",
            retained,
            excess);
        host.AppendServerChild(host.Root, serverList);
        ElementNode client = new(
            new QualifiedName("ul"),
            children:
            [
                new ElementNode(
                    new QualifiedName("li"),
                    children: [new TextNode("retained")]),
            ]);
        List<string> warnings = [];

        RendererFactory.CreateRenderer(host.Options).Hydrate(
            client,
            host.Root,
            CreateApplication(client, warnings));

        host.Root.Children.ShouldBe([serverList]);
        serverList.Children.ShouldBe([retained]);
        retained.Parent.ShouldBeSameAs(serverList);
        excess.Parent.ShouldBeNull();
        warnings.ShouldHaveSingleItem().ShouldContain("more child nodes");
    }

    [Fact]
    public void Hydrate_MissingReaderAndAlreadyMountedRoot_RejectInvalidEntryStates()
    {
        using var parityHost = new RendererParityHost();
        Renderer<RendererParityNode> renderer = parityHost.CreateRenderer();

        Should.Throw<NotSupportedException>(
            () => renderer.Hydrate(new TextNode("client"), parityHost.Container));

        HydrationWalkerFakeHost hydrationHost = new();
        HydrationWalkerHostNode server = hydrationHost.CreateServerText("ready");
        hydrationHost.AppendServerChild(hydrationHost.Root, server);
        Renderer<HydrationWalkerHostNode> hydrationRenderer =
            RendererFactory.CreateRenderer(hydrationHost.Options);
        var client = new TextNode("ready");
        hydrationRenderer.Hydrate(client, hydrationHost.Root);

        Should.Throw<InvalidOperationException>(
            () => hydrationRenderer.Hydrate(client, hydrationHost.Root));
    }

    private static ElementNode Element(string classValue, string styleValue) =>
        new(
            new QualifiedName("article"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("class"), classValue),
                ElementBinding.Attribute(new QualifiedName("style"), styleValue),
            ]);

    private static ApplicationContext CreateApplication(
        VirtualNode root,
        List<string> warnings) =>
        new(
            new ApplicationOptions
            {
                RootComponent = root,
                WarnHandler = warnings.Add,
            });
}
