using Assimalign.Vue.Shared;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/transforms/vBind.spec.ts: v-bind prop emission,
// class/style normalization, dynamic-argument FULL_PROPS escalation, and .prop/.attr/.camel modifiers.
public class VBindTransformTests
{
    [Fact]
    public void VBind_StaticArgument_EmitsProp()
    {
        var result = TransformTestHelpers.Transform("<div :id=\"x\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        props.Property("id").Value.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("x");
    }

    [Fact]
    public void VBind_DynamicClass_NormalizesAndFlagsClass()
    {
        var result = TransformTestHelpers.Transform("<div :class=\"cls\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.Class);
        var classValue = codegen.Props.ShouldBeOfType<ObjectExpression>().Property("class").Value.ShouldBeOfType<CallExpression>();
        classValue.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("normalizeClass");
    }

    [Fact]
    public void VBind_DynamicStyle_NormalizesAndFlagsStyle()
    {
        var result = TransformTestHelpers.Transform("<div :style=\"sty\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.Style);
        codegen.Props.ShouldBeOfType<ObjectExpression>().Property("style").Value.ShouldBeOfType<CallExpression>()
            .Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("normalizeStyle");
    }

    [Fact]
    public void VBind_CollectsDynamicPropNamesAndFlagsProps()
    {
        var result = TransformTestHelpers.Transform("<div :foo=\"x\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.Props);
        codegen.DynamicProps.ShouldBe("[\"foo\"]");
    }

    [Fact]
    public void VBind_DynamicArgument_EscalatesToFullProps()
    {
        var result = TransformTestHelpers.Transform("<div :[key]=\"x\"></div>");

        result.CodegenNode.ShouldBeOfType<VNodeCall>().ShouldHavePatchFlag(PatchFlags.FullProps);
    }

    [Fact]
    public void VBind_PropModifier_PrefixesArgumentWithDot()
    {
        var result = TransformTestHelpers.Transform("<div :id.prop=\"x\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        props.Property(".id").ShouldNotBeNull();
    }

    [Fact]
    public void VBind_CamelModifier_CamelizesStaticArgument()
    {
        var result = TransformTestHelpers.Transform("<div :view-box.camel=\"x\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VNodeCall>().Props.ShouldBeOfType<ObjectExpression>();
        props.Property("viewBox").ShouldNotBeNull();
    }

    [Fact]
    public void VBind_ObjectSpread_MergesProps()
    {
        // v-bind without argument (v-bind="obj") merges via mergeProps and escalates to FULL_PROPS.
        var result = TransformTestHelpers.Transform("<div v-bind=\"obj\" id=\"a\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.FullProps);
        codegen.Props.ShouldBeOfType<CallExpression>().Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("mergeProps");
    }
}
