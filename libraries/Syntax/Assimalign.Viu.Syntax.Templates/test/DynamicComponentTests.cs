using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Component type resolution: these cases ARE the contract for how a tag resolves to a component, a
// built-in, or a dynamic component (<component :is>), and what each form emits.
public class DynamicComponentTests
{
    [Fact]
    public void DynamicComponent_WithBoundIs_CompilesToResolveDynamicComponent()
    {
        var result = TransformTestHelpers.Transform("<component :is=\"foo\"></component>");

        var codegen = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>();
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

        var tag = result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Tag.ShouldBeOfType<CallExpression>();
        tag.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("resolveDynamicComponent");
        var expression = tag.Arguments[0].ShouldBeOfType<SimpleExpressionNode>();
        expression.Content.ShouldBe("foo");
        expression.IsStatic.ShouldBeTrue();
    }

    [Fact]
    public void UserComponent_CompilesToResolveComponent()
    {
        var result = TransformTestHelpers.Transform("<MyComponent></MyComponent>");

        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Tag.ShouldBe("_component_MyComponent");
        result.ShouldUseHelper("resolveComponent");
        result.Components.ShouldContain("MyComponent");
    }

    [Fact]
    public void BuiltInComponent_ResolvesToHelperSymbol()
    {
        var result = TransformTestHelpers.Transform("<Teleport to=\"#modal\"><div></div></Teleport>");

        result.CodegenNode.ShouldBeOfType<VirtualNodeCall>().Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Teleport");
    }
}
