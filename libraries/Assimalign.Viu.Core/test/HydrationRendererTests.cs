using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins mismatch recovery from Vue 3.5's hydration walker:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/hydration.ts.
/// </summary>
public sealed class HydrationRendererTests
{
    [Fact]
    public void Hydrate_TextMismatch_WarnsAndCorrectsTheAdoptedNodeInPlace()
    {
        FakeHost host = new();
        FakeHostNode serverText = host.CreateServerText("server");
        FakeHostNode serverElement =
            host.CreateServerElement("div", serverText);
        host.AppendServerChild(host.Root, serverElement);
        IElementComponent client = ComponentTree.Element(
            "div",
            children: [ComponentTree.Text("client")]);
        List<string> warnings = [];
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, Application(client, warnings));

        warnings.ShouldContain(
            warning => warning.Contains(
                "Hydration text mismatch",
                StringComparison.Ordinal));
        host.Root.Children.Single().ShouldBeSameAs(serverElement);
        serverText.Content.ShouldBe("client");
        host.Operations.ShouldContain(
            $"text:{serverText.Identifier}:server:client");
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("remove:", StringComparison.Ordinal));
        host.Operations.ShouldNotContain(
            operation => operation.StartsWith("create:", StringComparison.Ordinal));
    }

    [Fact]
    public void Hydrate_ElementTagMismatch_ReplacesOnlyTheMismatchedSubtree()
    {
        FakeHost host = new();
        FakeHostNode serverSpan = host.CreateServerElement(
            "span",
            host.CreateServerText("content"));
        FakeHostNode serverRoot =
            host.CreateServerElement("div", serverSpan);
        host.AppendServerChild(host.Root, serverRoot);
        IElementComponent client = ComponentTree.Element(
            "div",
            children:
            [
                ComponentTree.Element(
                    "p",
                    children: [ComponentTree.Text("content")]),
            ]);
        List<string> warnings = [];
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, Application(client, warnings));

        warnings.ShouldContain(
            warning => warning.Contains(
                "Hydration node mismatch",
                StringComparison.Ordinal));
        host.Root.Children.Single().ShouldBeSameAs(serverRoot);
        FakeHostNode replacement = serverRoot.Children.Single();
        replacement.ShouldNotBeSameAs(serverSpan);
        replacement.Content.ShouldBe("p");
        host.Text(replacement).ShouldBe("content");
        serverSpan.Parent.ShouldBeNull();
        host.Operations.ShouldContain($"remove:{serverSpan.Identifier}");
        host.Operations.Count(
                operation => operation.StartsWith(
                    "create:Element:",
                    StringComparison.Ordinal))
            .ShouldBe(1);
    }

    [Fact]
    public void Hydrate_ExcessElementChildren_WarnsAndRemovesOnlyTheExcessRange()
    {
        FakeHost host = new();
        FakeHostNode retained = host.CreateServerElement(
            "li",
            host.CreateServerText("a"));
        FakeHostNode excess = host.CreateServerElement(
            "li",
            host.CreateServerText("b"));
        FakeHostNode serverList =
            host.CreateServerElement("ul", retained, excess);
        host.AppendServerChild(host.Root, serverList);
        IElementComponent client = ComponentTree.Element(
            "ul",
            children:
            [
                ComponentTree.Element(
                    "li",
                    children: [ComponentTree.Text("a")]),
            ]);
        List<string> warnings = [];
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, Application(client, warnings));

        warnings.ShouldContain(
            warning => warning.Contains(
                "more child nodes",
                StringComparison.Ordinal));
        serverList.Children.ShouldBe([retained]);
        retained.Parent.ShouldBeSameAs(serverList);
        excess.Parent.ShouldBeNull();
        host.Operations.Where(
                operation => operation.StartsWith(
                    "remove:",
                    StringComparison.Ordinal))
            .ShouldBe([$"remove:{excess.Identifier}"]);
    }

    [Fact]
    public void Hydrate_ExcessRootChildren_WarnsAndRemovesOnlyTheTrailingRoots()
    {
        FakeHost host = new();
        FakeHostNode retained = host.CreateServerElement("main");
        FakeHostNode firstExcess = host.CreateServerElement("aside");
        FakeHostNode secondExcess = host.CreateServerText("tail");
        host.AppendServerChild(host.Root, retained);
        host.AppendServerChild(host.Root, firstExcess);
        host.AppendServerChild(host.Root, secondExcess);
        IElementComponent client = ComponentTree.Element("main");
        List<string> warnings = [];
        Renderer<FakeHostNode> renderer =
            RendererFactory.CreateRenderer(host.Options);

        renderer.Hydrate(client, host.Root, Application(client, warnings));

        warnings.ShouldContain(
            warning => warning.Contains(
                "extra root nodes",
                StringComparison.Ordinal));
        host.Root.Children.ShouldBe([retained]);
        firstExcess.Parent.ShouldBeNull();
        secondExcess.Parent.ShouldBeNull();
        host.Operations.Where(
                operation => operation.StartsWith(
                    "remove:",
                    StringComparison.Ordinal))
            .ShouldBe(
            [
                $"remove:{firstExcess.Identifier}",
                $"remove:{secondExcess.Identifier}",
            ]);
    }

    private static IApplicationContext Application(
        IComponent root,
        List<string> warnings)
    {
        return new ApplicationContext(
            root,
            new ComponentFactory(Array.Empty<ComponentRegistration>()),
            new EmptyServiceProvider())
        {
            WarnHandler = warnings.Add,
        };
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
