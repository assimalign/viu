using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Pins the host reads a component update performs. A root that patches through its existing
/// native handle never asks the host for its parent or next sibling, because on a buffered host
/// each such read flushes the pending command frame; a root that may be replaced still resolves
/// both so the replacement lands beside the right sibling. Specified by <c>[RND-IO-1]</c> and
/// <c>[V01.01.11.07]</c>.
/// </summary>
public sealed class ComponentUpdateHostReadTests
{
    [Theory]
    [InlineData("span")]
    [InlineData("text")]
    public void Update_RootKeepsItsNativeType_PerformsNoParentOrSiblingReads(string rootKind)
    {
        using var host = new RendererParityHost();
        Reference<string> kind = Reactive.Reference(rootKind);
        Reference<string> text = Reactive.Reference("before");
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        ElementNode root = MountProbe(host, renderer, kind, text);
        host.ResetOperationCounts();

        text.Value = "after";
        host.RunUntilIdle();

        RendererParityNode rootHost = host.Container.Children.ShouldHaveSingleItem();
        rootHost.DescendantText.ShouldBe("aftertail");
        host.ParentReadCount.ShouldBe(0);
        host.SiblingReadCount.ShouldBe(0);
        host.TextChangeCount.ShouldBe(1);
        host.InsertCount.ShouldBe(0);
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
        _ = root;
    }

    [Theory]
    [InlineData("span", "strong")]
    [InlineData("text", "span")]
    [InlineData("span", "text")]
    public void Update_RootChangesNativeType_ReadsParentOnceAndKeepsFollowingSibling(
        string initialKind,
        string nextKind)
    {
        using var host = new RendererParityHost();
        Reference<string> kind = Reactive.Reference(initialKind);
        Reference<string> text = Reactive.Reference("before");
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        MountProbe(host, renderer, kind, text);
        RendererParityNode rootHost = host.Container.Children.ShouldHaveSingleItem();
        RendererParityNode tail = rootHost.Children[1];
        host.ResetOperationCounts();

        kind.Value = nextKind;
        host.RunUntilIdle();

        rootHost.Children.Count.ShouldBe(2);
        rootHost.Children[1].ShouldBeSameAs(tail);
        rootHost.DescendantText.ShouldBe("beforetail");
        if (nextKind == "text")
        {
            rootHost.Children[0].Kind.ShouldBe(RendererParityNodeKind.Text);
        }
        else
        {
            rootHost.Children[0].Description.ShouldBe(nextKind);
        }

        host.ParentReadCount.ShouldBe(1);
        host.SiblingReadCount.ShouldBeGreaterThan(0);
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    private static ElementNode MountProbe(
        RendererParityHost host,
        Renderer<RendererParityNode> renderer,
        Reference<string> kind,
        Reference<string> text)
    {
        ComponentReference reference = ComponentReference.ForType(typeof(ProbeComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new ProbeComponent(kind, text)));
        var root = new ElementNode(
            new QualifiedName("root"),
            children: [new ComponentNode(reference), new TextNode("tail")]);
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = root,
                Components = components,
            });
        renderer.Render(root, host.Container, application);
        host.RunUntilIdle();
        host.Container.Children.ShouldHaveSingleItem().DescendantText.ShouldBe("beforetail");
        return root;
    }

    private sealed class ProbeComponent : IComponent
    {
        private readonly Reference<string> _kind;
        private readonly Reference<string> _text;

        internal ProbeComponent(Reference<string> kind, Reference<string> text)
        {
            _kind = kind;
            _text = text;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return _ => _kind.Value == "text"
                ? new TextNode(_text.Value)
                : new ElementNode(
                    new QualifiedName(_kind.Value),
                    children: [new TextNode(_text.Value)]);
        }
    }
}
