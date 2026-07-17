using Assimalign.Vue.RuntimeCore.VirtualDom;
using Shouldly;
using Xunit;

namespace Assimalign.Vue.RuntimeCore.Tests.VirtualDom;

public sealed class VirtualDomRendererTests
{
    [Fact]
    public void Render_MountsElementTreeThroughAdapter()
    {
        var adapter = new FakeDomAdapter();
        var renderer = new VirtualDomRenderer<FakeNode>(adapter);
        var container = adapter.CreateElement("root");

        renderer.Render(container, V.H("div", V.Props(("className", "card")), V.Text("hello")));

        var div = container.Children.ShouldHaveSingleItem();
        div.TagName.ShouldBe("div");
        div.Properties["className"].ShouldBe("card");
        div.Children.ShouldHaveSingleItem().Text.ShouldBe("hello");
    }

    [Fact]
    public void Render_PatchesTextInPlaceOnSecondRender()
    {
        var adapter = new FakeDomAdapter();
        var renderer = new VirtualDomRenderer<FakeNode>(adapter);
        var container = adapter.CreateElement("root");

        renderer.Render(container, V.H("span", V.Text("one")));
        var mountedSpan = container.Children.ShouldHaveSingleItem();

        renderer.Render(container, V.H("span", V.Text("two")));

        container.Children.ShouldHaveSingleItem().ShouldBeSameAs(mountedSpan);
        mountedSpan.Children.ShouldHaveSingleItem().Text.ShouldBe("two");
    }

    [Fact]
    public void Render_RemovesPropertiesDroppedFromNextTree()
    {
        var adapter = new FakeDomAdapter();
        var renderer = new VirtualDomRenderer<FakeNode>(adapter);
        var container = adapter.CreateElement("root");

        renderer.Render(container, V.H("input", V.Props(("disabled", true), ("value", "a"))));
        renderer.Render(container, V.H("input", V.Props(("value", "b"))));

        var input = container.Children.ShouldHaveSingleItem();
        input.Properties.ContainsKey("disabled").ShouldBeFalse();
        input.Properties["value"].ShouldBe("b");
    }

    [Fact]
    public void Unmount_DestroysMountedNodes()
    {
        var adapter = new FakeDomAdapter();
        var renderer = new VirtualDomRenderer<FakeNode>(adapter);
        var container = adapter.CreateElement("root");

        renderer.Render(container, V.H("div", V.Text("x")));
        renderer.Unmount(container);

        container.Children.ShouldBeEmpty();
        adapter.DestroyedCount.ShouldBe(2);
    }
}
