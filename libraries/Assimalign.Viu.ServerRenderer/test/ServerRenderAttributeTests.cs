using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Pins Viu's attribute-serialization contract for
/// <c>SsrRenderAttrs</c>/<c>SsrRenderAttr</c>/<c>SsrRenderDynamicAttr</c>: boolean attributes render
/// by presence, enumerated values render their string, an SSR-unsafe dynamic name is dropped rather
/// than escaped, <c>class</c>/<c>style</c> route through their normalizers, and renderer metadata,
/// event handlers, and a textarea's <c>value</c> are excluded ([SSR-6]).
/// </summary>
public class ServerRenderAttributeTests
{
    [Fact]
    public void SsrRenderAttr_EscapesValue()
        => ServerRender.SsrRenderAttr("title", "a\"<b").ShouldBe(" title=\"a&quot;&lt;b\"");

    [Fact]
    public void SsrRenderAttr_NonRenderableValue_IsSkipped()
        => ServerRender.SsrRenderAttr("data-x", new object()).ShouldBe(string.Empty);

    [Fact]
    public void SsrRenderDynamicAttr_BooleanAttribute_RendersByPresence()
    {
        // disabled is a boolean attribute: truthy -> bare name, falsy -> omitted.
        ServerRender.SsrRenderDynamicAttr("disabled", true).ShouldBe(" disabled");
        ServerRender.SsrRenderDynamicAttr("disabled", false).ShouldBe(string.Empty);
        ServerRender.SsrRenderDynamicAttr("disabled", "").ShouldBe(" disabled");
    }

    [Fact]
    public void SsrRenderDynamicAttr_EmptyStringValue_RendersBareName()
        => ServerRender.SsrRenderDynamicAttr("data-flag", "").ShouldBe(" data-flag");

    [Fact]
    public void SsrRenderDynamicAttr_EnumeratedAttribute_RendersValue()
        // contenteditable is enumerated, not boolean: the value must be serialized.
        => ServerRender.SsrRenderDynamicAttr("contenteditable", true).ShouldBe(" contenteditable=\"true\"");

    [Fact]
    public void SsrRenderDynamicAttr_UnsafeAttributeName_IsSkipped()
    {
        // A name carrying '>', '=', quotes, or whitespace is skipped rather than escaped (injection gate).
        ServerRender.SsrRenderDynamicAttr("foo>bar", "x").ShouldBe(string.Empty);
        ServerRender.SsrRenderDynamicAttr("a b", "x").ShouldBe(string.Empty);
        ServerRender.SsrRenderDynamicAttr("onclick=alert(1)", "x").ShouldBe(string.Empty);
    }

    [Fact]
    public void SsrRenderDynamicAttr_LowercasesUnmappedName_ButPreservesSvgAndCustomElement()
    {
        ServerRender.SsrRenderDynamicAttr("viewBox", "0 0 1 1").ShouldBe(" viewbox=\"0 0 1 1\"");
        // SVG tags and custom elements preserve the author's casing.
        ServerRender.SsrRenderDynamicAttr("viewBox", "0 0 1 1", "svg").ShouldBe(" viewBox=\"0 0 1 1\"");
        ServerRender.SsrRenderDynamicAttr("myAttr", "x", "my-widget").ShouldBe(" myAttr=\"x\"");
    }

    [Fact]
    public void SsrRenderAttrs_SkipsReservedHandlerAndDotPrefixedProperties()
    {
        var attributes = TestTree.Attributes(
            ("id", "app"),
            ("key", "k"),
            ("ref", "r"),
            ("ref_for", true),
            ("innerHTML", "<b>"),
            ("textContent", "t"),
            ("onClick", (System.Action)(() => { })),
            (".prop", "forced-property"));
        ServerRender.SsrRenderAttrs(attributes).ShouldBe(" id=\"app\"");
    }

    [Fact]
    public void SsrRenderAttrs_CaretPrefix_ForcesAttributeAndIsStripped()
        => ServerRender.SsrRenderAttrs(TestTree.Attributes(("^foo", "bar"))).ShouldBe(" foo=\"bar\"");

    [Fact]
    public void SsrRenderAttrs_ClassAndStyle_RouteThroughNormalizers()
    {
        var attributes = TestTree.Attributes(
            ("class", new System.Collections.Generic.List<object?> { "a", "b" }),
            ("style", new System.Collections.Generic.Dictionary<string, object?> { ["color"] = "red" }));
        ServerRender.SsrRenderAttrs(attributes).ShouldBe(" class=\"a b\" style=\"color:red;\"");
    }

    [Fact]
    public void SsrRenderAttrs_ClassName_CoercesDirectlyToString()
        => ServerRender.SsrRenderAttrs(TestTree.Attributes(("className", "a b")))
            .ShouldBe(" class=\"a b\"");

    [Fact]
    public void SsrRenderAttrs_TextareaValue_IsSkipped()
        // <textarea>'s value is its text content, not an attribute.
        => ServerRender.SsrRenderAttrs(TestTree.Attributes(("value", "hi")), "textarea")
            .ShouldBe(string.Empty);
}
