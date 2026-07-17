using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeCore.Tests;

// Pins the vnode model contract of @vue/runtime-core's vnode.ts (createVNode/h, cloneVNode,
// mergeProps, isVNode, normalizeVNode) — https://vuejs.org/api/render-function.html.
public class VirtualNodeTests
{
    [Fact]
    public void Element_WithTextChildren_SetsElementAndTextChildrenShapeFlags()
    {
        var node = VirtualNodeFactory.Element("div", "hello");

        node.Type.ShouldBe(VirtualNodeType.Element);
        node.ElementTag.ShouldBe("div");
        node.TextChildren.ShouldBe("hello");
        node.ShapeFlag.ShouldBe(ShapeFlags.Element | ShapeFlags.TextChildren);
    }

    [Fact]
    public void Element_WithArrayChildren_SetsArrayChildrenShapeFlagAndNormalizesNullsToComments()
    {
        // Upstream normalizeVNode: null/undefined children become comment placeholders.
        var node = VirtualNodeFactory.Element("div", VirtualNodeFactory.Text("a"), null);

        node.ShapeFlag.ShouldBe(ShapeFlags.Element | ShapeFlags.ArrayChildren);
        node.ArrayChildren.ShouldNotBeNull();
        node.ArrayChildren.Length.ShouldBe(2);
        node.ArrayChildren[0].Type.ShouldBe(VirtualNodeType.Text);
        node.ArrayChildren[1].Type.ShouldBe(VirtualNodeType.Comment);
    }

    [Fact]
    public void Element_WithNoChildren_HasElementShapeFlagOnly()
    {
        var node = VirtualNodeFactory.Element("div");

        node.ShapeFlag.ShouldBe(ShapeFlags.Element);
        node.ArrayChildren.ShouldBeNull();
    }

    [Fact]
    public void Element_KeyAndReferenceAreExtractedFromPropertiesAtCreation()
    {
        var reference = new object();
        var node = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("key", "k1"), ("ref", reference), ("class", "c")));

        node.Key.ShouldBe("k1");
        node.Reference.ShouldBeSameAs(reference);
    }

    [Fact]
    public void Element_PatchFlagAndDynamicPropertiesRoundTrip()
    {
        var dynamicProperties = new[] { "id", "title" };
        var node = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("id", "a")),
            (VirtualNode?[]?)null,
            PatchFlags.Props,
            dynamicProperties);

        node.PatchFlag.ShouldBe(PatchFlags.Props);
        node.DynamicProperties.ShouldBeSameAs(dynamicProperties);
        node.DynamicChildren.ShouldBeNull();
        node.DynamicChildren = new List<VirtualNode>();
        node.DynamicChildren.ShouldNotBeNull();
    }

    [Fact]
    public void TextCommentStaticAndFragment_CarryTheirKinds()
    {
        VirtualNodeFactory.Text("t").Type.ShouldBe(VirtualNodeType.Text);
        VirtualNodeFactory.Text("t", PatchFlags.Text).PatchFlag.ShouldBe(PatchFlags.Text);
        VirtualNodeFactory.Comment("c").Type.ShouldBe(VirtualNodeType.Comment);
        VirtualNodeFactory.Static("<b>s</b>").Type.ShouldBe(VirtualNodeType.Static);
        VirtualNodeFactory.Static("<b>s</b>").TextChildren.ShouldBe("<b>s</b>");

        var fragment = VirtualNodeFactory.Fragment(VirtualNodeFactory.Text("a"));
        fragment.Type.ShouldBe(VirtualNodeType.Fragment);
        fragment.ShapeFlag.ShouldBe(ShapeFlags.ArrayChildren);

        var keyedFragment = VirtualNodeFactory.Fragment([VirtualNodeFactory.Text("a")], "fk", PatchFlags.StableFragment);
        keyedFragment.Key.ShouldBe("fk");
        keyedFragment.PatchFlag.ShouldBe(PatchFlags.StableFragment);
    }

    [Fact]
    public void IsVirtualNode_IdentifiesVnodesOnly()
    {
        VirtualNodeFactory.IsVirtualNode(VirtualNodeFactory.Text("t")).ShouldBeTrue();
        VirtualNodeFactory.IsVirtualNode("text").ShouldBeFalse();
        VirtualNodeFactory.IsVirtualNode(null).ShouldBeFalse();
    }

    [Fact]
    public void Clone_SharesChildrenCopiesBackPointersAndMergesExtraProperties()
    {
        // Upstream cloneVNode: children are shared, el is copied, extraProps merge via mergeProps.
        var node = VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("class", "a"), ("id", "x")),
            VirtualNodeFactory.Text("child"));
        node.El = new object();

        var clone = VirtualNodeFactory.Clone(node, VirtualNodeFactory.Properties(("class", "b")));

        clone.ShouldNotBeSameAs(node);
        clone.ElementTag.ShouldBe("div");
        clone.ArrayChildren.ShouldBeSameAs(node.ArrayChildren);
        clone.El.ShouldBeSameAs(node.El);
        clone.Properties.ShouldNotBeNull();
        clone.Properties["class"].ShouldBe("a b");
        clone.Properties["id"].ShouldBe("x");
        node.Properties!["class"].ShouldBe("a");
    }

    [Fact]
    public void Normalize_NullBecomesComment_UnmountedPassesThrough_MountedIsCloned()
    {
        // Upstream normalizeVNode + cloneIfMounted: a vnode with a live el is cloned on reuse so
        // remounting cannot corrupt the original's el.
        VirtualNodeFactory.Normalize(null).Type.ShouldBe(VirtualNodeType.Comment);

        var unmounted = VirtualNodeFactory.Text("t");
        VirtualNodeFactory.Normalize(unmounted).ShouldBeSameAs(unmounted);

        var mounted = VirtualNodeFactory.Text("t");
        mounted.El = new object();
        var normalized = VirtualNodeFactory.Normalize(mounted);
        normalized.ShouldNotBeSameAs(mounted);
        normalized.TextChildren.ShouldBe("t");
    }

    [Fact]
    public void MergeProperties_ConcatenatesClassStrings()
    {
        // Upstream mergeProps: class values merge — for strings, space concatenation.
        var merged = VirtualNodeFactory.MergeProperties(
            VirtualNodeFactory.Properties(("class", "a")),
            VirtualNodeFactory.Properties(("class", "b")));

        merged["class"].ShouldBe("a b");
    }

    [Fact]
    public void MergeProperties_MergesStyleStringsAndDictionaries()
    {
        var stringMerged = VirtualNodeFactory.MergeProperties(
            VirtualNodeFactory.Properties(("style", "color:red")),
            VirtualNodeFactory.Properties(("style", "width:1px")));
        stringMerged["style"].ShouldBe("color:red;width:1px");

        var mapMerged = VirtualNodeFactory.MergeProperties(
            VirtualNodeFactory.Properties(("style", new Dictionary<string, object?> { ["color"] = "red", ["width"] = "1px" })),
            VirtualNodeFactory.Properties(("style", new Dictionary<string, object?> { ["color"] = "blue" })));
        var map = mapMerged["style"].ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();
        map!["color"].ShouldBe("blue");
        map["width"].ShouldBe("1px");
    }

    [Fact]
    public void MergeProperties_ChainsEventHandlersIntoMulticast()
    {
        // Upstream mergeProps: onX handlers concatenate so every handler is invoked.
        var calls = new List<string>();
        var merged = VirtualNodeFactory.MergeProperties(
            VirtualNodeFactory.Properties(("onClick", (Action)(() => calls.Add("first")))),
            VirtualNodeFactory.Properties(("onClick", (Action)(() => calls.Add("second")))));

        var handler = merged["onClick"].ShouldBeAssignableTo<Action>();
        handler!();

        calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public void MergeProperties_LaterSourcesWinForOrdinaryValues()
    {
        var merged = VirtualNodeFactory.MergeProperties(
            VirtualNodeFactory.Properties(("id", "a"), ("title", "t")),
            null,
            VirtualNodeFactory.Properties(("id", "b")));

        merged["id"].ShouldBe("b");
        merged["title"].ShouldBe("t");
    }

    [Fact]
    public void IsEventListenerName_MatchesOnFollowedByUpperCase()
    {
        // Upstream isOn: /^on[^a-z]/.
        VirtualNodeFactory.IsEventListenerName("onClick").ShouldBeTrue();
        VirtualNodeFactory.IsEventListenerName("onclick").ShouldBeFalse();
        VirtualNodeFactory.IsEventListenerName("on").ShouldBeFalse();
        VirtualNodeFactory.IsEventListenerName("once").ShouldBeFalse();
    }
}
