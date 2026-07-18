using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/transforms/transformElement.spec.ts: component
// type resolution, including dynamic components (<component :is>) and built-in components.
public class DynamicComponentTests
{
    [Fact]
    public void DynamicComponent_WithBoundIs_CompilesToResolveDynamicComponent()
    {
        var result = TransformTestHelpers.Transform("<component :is=\"foo\"></component>");

        var codegen = result.CodegenNode.ShouldBeOfType<VNodeCall>();
        var tag = codegen.Tag.ShouldBeOfType<CallExpression>();
        tag.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("resolveDynamicComponent");
        tag.Arguments[0].ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("foo");
        codegen.IsBlock.ShouldBeTrue();
        result.ShouldUseHelper("resolveDynamicComponent");
    }

    [Fact]
    public void DynamicComponent_WithStaticIs_UsesStaticName()
    {
        var result = TransformTestHelpers.Transform("<component is=\"foo\"></component>");

        var tag = result.CodegenNode.ShouldBeOfType<VNodeCall>().Tag.ShouldBeOfType<CallExpression>();
        tag.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("resolveDynamicComponent");
        var expression = tag.Arguments[0].ShouldBeOfType<SimpleExpressionNode>();
        expression.Content.ShouldBe("foo");
        expression.IsStatic.ShouldBeTrue();
    }

    [Fact]
    public void UserComponent_CompilesToResolveComponent()
    {
        var result = TransformTestHelpers.Transform("<MyComponent></MyComponent>");

        result.CodegenNode.ShouldBeOfType<VNodeCall>().Tag.ShouldBe("_component_MyComponent");
        result.ShouldUseHelper("resolveComponent");
        result.Components.ShouldContain("MyComponent");
    }

    [Fact]
    public void BuiltInComponent_ResolvesToHelperSymbol()
    {
        var result = TransformTestHelpers.Transform("<Teleport to=\"#modal\"><div></div></Teleport>");

        result.CodegenNode.ShouldBeOfType<VNodeCall>().Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Teleport");
    }
}
