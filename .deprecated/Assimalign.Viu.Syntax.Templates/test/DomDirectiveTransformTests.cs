using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Ported from vuejs/core packages/compiler-dom/__tests__/transforms/{vShow,vHtml,vText}.spec.ts, plus the
// v-pre (parser-driven) and v-cloak (noop) integration cases.
public class DomDirectiveTransformTests
{
    [Fact]
    public void VShow_EmitsShowRuntimeDirective()
    {
        var result = TransformTestHelpers.Transform("<div v-show=\"visible\"></div>");

        result.ShouldUseHelper("vShow");
        result.CodegenNode.ShouldBeOfType<VNodeCall>().Directives.ShouldNotBeNull();
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

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        props.Property("innerHTML").Value.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("raw");
    }

    [Fact]
    public void VHtml_WithChildren_ReportsErrorAndClearsChildren()
    {
        var result = TransformTestHelpers.Transform("<div v-html=\"raw\"><span></span></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVHtmlWithChildren);
        result.CodegenNode.ShouldBeOfType<VNodeCall>().Children.ShouldBeNull();
    }

    [Fact]
    public void VText_EmitsTextContentPropWrappedInToDisplayString()
    {
        var result = TransformTestHelpers.Transform("<div v-text=\"msg\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        var value = props.Property("textContent").Value.ShouldBeOfType<CallExpression>();
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
        result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeNull();
    }

    [Fact]
    public void VPre_PreservesInterpolationDelimitersAsLiteralText()
    {
        // The parser handles v-pre: {{ raw }} inside a v-pre subtree becomes literal text, and no
        // toDisplayString helper is emitted. The transform must leave it untouched.
        var result = TransformTestHelpers.Transform("<div v-pre>{{ raw }}</div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        codegen.Children.ShouldBeOfType<TextNode>().Content.ShouldBe("{{ raw }}");
        result.UsesHelper("toDisplayString").ShouldBeFalse();
    }
}
