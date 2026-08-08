using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// The DOM directive transforms: v-show, v-html, and v-text, plus the v-pre (parser-driven) and v-cloak
// (contributes nothing) integration cases. These cases ARE the contract for what each directive
// contributes to an element's props and which runtime directive, if any, it requests.
public class DomDirectiveTransformTests
{
    [Fact]
    public void VShow_EmitsShowRuntimeDirective()
    {
        var result = TransformTestHelpers.Transform("<div v-show=\"visible\"></div>");

        result.ShouldUseHelper("vShow");
        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Directives.ShouldNotBeNull();
    }

    [Fact]
    public void VShow_NoExpression_ReportsError()
    {
        TransformTestHelpers.Transform("<div v-show></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVShowNoExpression);
    }

    [Fact]
    public void VHtml_EmitsInnerHtmlProp()
    {
        var result = TransformTestHelpers.Transform("<div v-html=\"raw\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("innerHTML").Value.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("raw");
    }

    [Fact]
    public void VHtml_WithChildren_ReportsErrorAndClearsChildren()
    {
        var result = TransformTestHelpers.Transform("<div v-html=\"raw\"><span></span></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVHtmlWithChildren);
        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Children.ShouldBeNull();
    }

    [Fact]
    public void VText_EmitsTextContentPropWrappedInToDisplayString()
    {
        var result = TransformTestHelpers.Transform("<div v-text=\"msg\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        var value = props.ObjectProperty("textContent").Value.ShouldBeOfType<CallExpression>();
        value.Callee.ShouldBe("_toDisplayString");
    }

    [Fact]
    public void VText_WithChildren_ReportsError()
    {
        TransformTestHelpers.Transform("<div v-text=\"msg\"><span></span></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVTextWithChildren);
    }

    [Fact]
    public void VCloak_EmitsNoRuntimeProp()
    {
        var result = TransformTestHelpers.Transform("<div v-cloak></div>");

        // v-cloak contributes nothing at compile time; the element has no props.
        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeNull();
    }

    [Fact]
    public void VPre_PreservesInterpolationDelimitersAsLiteralText()
    {
        // The parser handles v-pre: {{ raw }} inside a v-pre subtree becomes literal text, and no
        // toDisplayString helper is emitted. The transform must leave it untouched.
        var result = TransformTestHelpers.Transform("<div v-pre>{{ raw }}</div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
        codegen.Children.ShouldBeOfType<TextNode>().Content.ShouldBe("{{ raw }}");
        result.UsesHelper("toDisplayString").ShouldBeFalse();
    }
}
