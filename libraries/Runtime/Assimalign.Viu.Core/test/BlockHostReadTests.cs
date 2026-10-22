using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Core.Tests;

public sealed class BlockHostReadTests
{
    [Theory]
    [InlineData("element")]
    [InlineData("text")]
    [InlineData("comment")]
    public void Block_StableNestedNativeIdentity_PatchesWithoutStructuralHostReads(string kind)
    {
        // [RND-BLOCK-5], [V01.01.11.07]: stable native updates must keep the command batch intact.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(Block(Child(kind, "before")), host.Container);
        host.ResetOperationCounts();

        renderer.Render(Block(Child(kind, "after")), host.Container);

        // Comment text is intentionally immutable after mount; native identity still needs no reads.
        host.Container.DescendantText.ShouldBe(kind == "comment" ? "beforetail" : "aftertail");
        host.ParentReadCount.ShouldBe(0);
        host.SiblingReadCount.ShouldBe(0);
        host.TextChangeCount.ShouldBe(kind == "comment" ? 0 : 1);
        renderer.Render(null, host.Container);
        host.Container.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Block_NativeTypeOrKeyChanges_ResolvesActualNestedParentAndPreservesFollowingSibling(bool changeKey)
    {
        // A replacement still needs the actual nested parent, rather than the outer block's handle.
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        renderer.Render(Block(new ElementNode(new QualifiedName("span"), children: [new TextNode("before")], key: 1)), host.Container);
        host.ResetOperationCounts();

        renderer.Render(Block(new ElementNode(new QualifiedName(changeKey ? "span" : "strong"), children: [new TextNode("after")], key: changeKey ? 2 : 1)), host.Container);

        host.ParentReadCount.ShouldBe(1);
        host.SiblingReadCount.ShouldBeGreaterThan(0);
        host.Container.DescendantText.ShouldBe("aftertail");
        RendererParityNode wrapper = host.Container.Children[0].Children[0];
        wrapper.Children.Count.ShouldBe(2);
        wrapper.Children[0].Description.ShouldBe(changeKey ? "span" : "strong");
        renderer.Render(null, host.Container);
    }

    private static ElementNode Block(VirtualNode dynamicChild) => new(
        new QualifiedName("root"),
        children: [new ElementNode(new QualifiedName("wrapper"), children: [dynamicChild, new TextNode("tail")])],
        renderPlan: new RenderPlan(PatchFlags.NeedPatch, dynamicChildren: [dynamicChild]));

    private static VirtualNode Child(string kind, string value) => kind switch
    {
        "text" => new TextNode(value),
        "comment" => new CommentNode(value),
        _ => new ElementNode(new QualifiedName("span"), children: [new TextNode(value)], renderPlan: new RenderPlan(PatchFlags.Text)),
    };
}
