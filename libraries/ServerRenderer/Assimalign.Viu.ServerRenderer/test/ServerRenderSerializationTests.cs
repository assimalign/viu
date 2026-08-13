using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerRenderSerializationTests
{
    [Fact]
    public void EscapeHtml_FiveCharacterContract_UsesExactEntities()
    {
        ServerRender.EscapeHtml("\"&'<> ").ShouldBe("&quot;&amp;&#39;&lt;&gt; ");
        ServerRender.EscapeHtml((string?)null).ShouldBeEmpty();
        ServerRender.EscapeHtml("plain").ShouldBe("plain");
    }

    [Fact]
    public void EscapeHtmlComment_Terminators_AreRepeatedlyRemoved()
    {
        ServerRender.EscapeHtmlComment("a-->b").ShouldBe("ab");
        ServerRender.EscapeHtmlComment("<!--nested-->").ShouldBe("nested");
        ServerRender.EscapeHtmlComment("--!<!--->").ShouldNotContain("-->");
        ServerRender.SsrRenderComment(string.Empty).ShouldBe(HydrationMarkers.EmptyComment);
    }

    [Fact]
    public void ClassAndStyleBindings_NormalizeBeforeEscaping()
    {
        ServerRender.SsrRenderClass(
            new object?[]
            {
                "base",
                new Dictionary<string, object?>
                {
                    ["active"] = true,
                    ["hidden"] = false,
                },
            }).ShouldBe("base active");
        ServerRender.SsrRenderStyle(
            new Dictionary<string, object?>
            {
                ["color"] = "red",
                ["fontSize"] = "12px",
            }).ShouldBe("color:red;font-size:12px;");
        ServerRender.SsrRenderStyle("\"><script>")
            .ShouldBe("&quot;&gt;&lt;script&gt;");
    }

    [Fact]
    public void DynamicAttribute_BooleanUnsafeAndCasingRules_AreApplied()
    {
        ServerRender.SsrRenderDynamicAttribute("disabled", true).ShouldBe(" disabled");
        ServerRender.SsrRenderDynamicAttribute("disabled", false).ShouldBeEmpty();
        ServerRender.SsrRenderDynamicAttribute("contenteditable", true)
            .ShouldBe(" contenteditable=\"true\"");
        ServerRender.SsrRenderDynamicAttribute("bad=name", "value").ShouldBeEmpty();
        ServerRender.SsrRenderDynamicAttribute("viewBox", "0 0 1 1")
            .ShouldBe(" viewbox=\"0 0 1 1\"");
        ServerRender.SsrRenderDynamicAttribute("viewBox", "0 0 1 1", "svg")
            .ShouldBe(" viewBox=\"0 0 1 1\"");
        ServerRender.SsrRenderDynamicAttribute("myAttribute", "x", "my-widget")
            .ShouldBe(" myAttribute=\"x\"");
    }

    [Fact]
    public void AttributeBindings_MetadataPropertyEventsAndUnsafeNames_AreSkipped()
    {
        ElementBinding[] bindings =
        [
            ElementBinding.Attribute(new QualifiedName("id"), "app"),
            ElementBinding.Attribute(new QualifiedName("key"), "identity"),
            ElementBinding.Attribute(new QualifiedName("onClick"), "ignored"),
            ElementBinding.Attribute(new QualifiedName("bad name"), "ignored"),
            ElementBinding.Property("title", "property"),
            ElementBinding.Event("click", (Action)(() => { })),
        ];

        ServerRender.SsrRenderAttributes(bindings, new QualifiedName("div"))
            .ShouldBe(" id=\"app\"");
    }

    [Fact]
    public void AttributeBindings_ClassStyleAndClassName_UseDistinctNormalizationPaths()
    {
        ElementBinding[] bindings =
        [
            ElementBinding.Attribute(
                new QualifiedName("class"),
                new object?[] { "a", "b" }),
            ElementBinding.Attribute(
                new QualifiedName("style"),
                new Dictionary<string, object?> { ["fontSize"] = "10px" }),
            ElementBinding.Attribute(new QualifiedName("className"), "raw"),
        ];

        ServerRender.SsrRenderAttributes(bindings, new QualifiedName("div"))
            .ShouldBe(" class=\"a b\" style=\"font-size:10px;\" class=\"raw\"");
    }

    [Fact]
    public async Task Element_ChildOverrideProperties_ControlSerializedContent()
    {
        ElementNode innerHtml = Element(
            "div",
            [ElementBinding.Property("innerHTML", "<b>raw</b>")],
            [new TextNode("ignored")]);
        ElementNode textContent = Element(
            "div",
            [ElementBinding.Property("textContent", "<escaped>")],
            [new TextNode("ignored")]);
        ElementNode textArea = Element(
            "textarea",
            [ElementBinding.Attribute(new QualifiedName("value"), "<value>")]);

        (await ServerRenderer.RenderToStringAsync(innerHtml))
            .ShouldBe("<div><b>raw</b></div>");
        (await ServerRenderer.RenderToStringAsync(textContent))
            .ShouldBe("<div>&lt;escaped&gt;</div>");
        (await ServerRenderer.RenderToStringAsync(textArea))
            .ShouldBe("<textarea>&lt;value&gt;</textarea>");
    }

    [Fact]
    public async Task Element_VoidAndNamespaceRules_PreserveRequiredShapes()
    {
        ElementNode lineBreak = Element("br", children: [new TextNode("ignored")]);
        ElementNode graphic = new(
            new QualifiedName("svg", "http://www.w3.org/2000/svg"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("viewBox"), "0 0 1 1"),
            ]);

        (await ServerRenderer.RenderToStringAsync(lineBreak)).ShouldBe("<br>");
        (await ServerRenderer.RenderToStringAsync(graphic))
            .ShouldBe("<svg viewBox=\"0 0 1 1\"></svg>");
    }

    [Fact]
    public async Task SsrRenderListAsync_SupportedSources_PreserveDefinedOrder()
    {
        List<(object? Value, object? Key)> observed = [];

        await ServerRender.SsrRenderListAsync(
            2,
            (value, key) =>
            {
                observed.Add((value, key));
                return Task.CompletedTask;
            });
        await ServerRender.SsrRenderListAsync(
            new[] { "a", "b" },
            (value, key) =>
            {
                observed.Add((value, key));
                return Task.CompletedTask;
            });

        observed.ShouldBe(
        [
            (1, 0),
            (2, 1),
            ("a", 0),
            ("b", 1),
        ]);
    }

    private static ElementNode Element(
        string name,
        IReadOnlyList<ElementBinding>? bindings = null,
        IReadOnlyList<VirtualNode>? children = null) =>
        new(new QualifiedName(name), bindings, children);
}
