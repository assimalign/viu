using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// These cases ARE the contract for v-bind prop emission,
// class/style normalization, dynamic-argument FULL_PROPS escalation, and .prop/.attr/.camel modifiers.
public class VBindTransformTests
{
    [Fact]
    public void VBind_StaticArgument_EmitsProp()
    {
        var result = TransformTestHelpers.Transform("<div :id=\"x\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("id").Value.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("x");
    }

    [Fact]
    public void VBind_DynamicClass_NormalizesAndFlagsClass()
    {
        var result = TransformTestHelpers.Transform("<div :class=\"cls\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.Class);
        var classValue = codegen.Properties.ShouldBeOfType<ObjectExpression>().ObjectProperty("class").Value.ShouldBeOfType<CallExpression>();
        classValue.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("normalizeClass");
    }

    [Fact]
    public void VBind_DynamicStyle_NormalizesAndFlagsStyle()
    {
        var result = TransformTestHelpers.Transform("<div :style=\"sty\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.Style);
        codegen.Properties.ShouldBeOfType<ObjectExpression>().ObjectProperty("style").Value.ShouldBeOfType<CallExpression>()
            .Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("normalizeStyle");
    }

    [Fact]
    public void VBind_CollectsDynamicPropertyNamesAndFlagsProperties()
    {
        var result = TransformTestHelpers.Transform("<div :foo=\"x\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.Properties);
        codegen.DynamicProperties.ShouldBe("[\"foo\"]");
    }

    [Fact]
    public void VBind_DynamicArgument_EscalatesToFullProperties()
    {
        var result = TransformTestHelpers.Transform("<div :[key]=\"x\"></div>");

        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().ShouldHavePatchFlag(PatchFlags.FullProperties);
    }

    [Fact]
    public void VBind_PropModifier_PrefixesArgumentWithDot()
    {
        var result = TransformTestHelpers.Transform("<div :id.prop=\"x\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty(".id").ShouldNotBeNull();
    }

    [Fact]
    public void VBind_CamelModifier_CamelizesStaticArgument()
    {
        var result = TransformTestHelpers.Transform("<div :view-box.camel=\"x\"></div>");

        var props = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Properties.ShouldBeOfType<ObjectExpression>();
        props.ObjectProperty("viewBox").ShouldNotBeNull();
    }

    [Fact]
    public void VBind_ObjectSpread_MergesProperties()
    {
        // v-bind without argument (v-bind="obj") merges via mergeProps and escalates to FULL_PROPS.
        var result = TransformTestHelpers.Transform("<div v-bind=\"obj\" id=\"a\"></div>");

        var codegen = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
        codegen.ShouldHavePatchFlag(PatchFlags.FullProperties);
        codegen.Properties.ShouldBeOfType<CallExpression>().Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("mergeProps");
    }
}
