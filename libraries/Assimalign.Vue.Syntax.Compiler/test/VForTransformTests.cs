using Assimalign.Vue.Shared;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/transforms/vFor.spec.ts: the ForNode alias
// decomposition, the renderList fragment codegen, and keyed/unkeyed fragment classification.
public class VForTransformTests
{
    [Fact]
    public void VFor_ValueOnly_DecomposesValueAlias()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"item in list\"></div>");

        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        forNode.Source.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("list");
        forNode.ValueAlias.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("item");
        forNode.KeyAlias.ShouldBeNull();
        forNode.ObjectIndexAlias.ShouldBeNull();
    }

    [Fact]
    public void VFor_ValueAndKey_DecomposesTuple()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"(item, index) in list\"></div>");

        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        forNode.ValueAlias.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("item");
        forNode.KeyAlias.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("index");
        forNode.ObjectIndexAlias.ShouldBeNull();
    }

    [Fact]
    public void VFor_ValueKeyIndex_DecomposesAllThree()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"(value, key, index) in obj\"></div>");

        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        forNode.ValueAlias.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("value");
        forNode.KeyAlias.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("key");
        forNode.ObjectIndexAlias.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("index");
    }

    [Fact]
    public void VFor_OfIterator_IsSupported()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"item of list\"></div>");

        result.SingleChild().ShouldBeOfType<ForNode>()
            .Source.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("list");
    }

    [Fact]
    public void VFor_Unkeyed_CompilesToUnkeyedFragmentBlock()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"item in list\"></div>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();

        var codegen = result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>();
        codegen.Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
        codegen.IsBlock.ShouldBeTrue();
        codegen.DisableTracking.ShouldBeTrue();
        codegen.ShouldHavePatchFlag(PatchFlags.UnkeyedFragment);
        result.ShouldUseHelper("renderList");

        var renderList = codegen.Children.ShouldBeOfType<CallExpression>();
        renderList.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("renderList");
        renderList.Arguments[0].ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("list");
        var iterator = renderList.Arguments[1].ShouldBeOfType<FunctionExpression>();
        iterator.Parameters[0].ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("item");
    }

    [Fact]
    public void VFor_Keyed_CompilesToKeyedFragmentBlock()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"item in list\" :key=\"item.id\"></div>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();

        result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>().ShouldHavePatchFlag(PatchFlags.KeyedFragment);
    }

    [Fact]
    public void TemplateVFor_MultipleChildren_WrapsInStableFragment()
    {
        var result = TransformTestHelpers.Transform("<template v-for=\"i in list\"><div></div><p></p></template>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        forNode.Children.Count.ShouldBe(2);

        var codegen = result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>();
        var renderList = codegen.Children.ShouldBeOfType<CallExpression>();
        var iterator = renderList.Arguments[1].ShouldBeOfType<FunctionExpression>();
        iterator.Returns.ShouldBeOfType<VNodeCall>().Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
    }

    [Fact]
    public void VFor_NoExpression_ReportsError()
    {
        TransformTestHelpers.Transform("<div v-for></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVForNoExpression);
    }

    [Fact]
    public void VFor_MalformedExpression_ReportsError()
    {
        TransformTestHelpers.Transform("<div v-for=\"nonsense\"></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVForMalformedExpression);
    }

    [Fact]
    public void TemplateVFor_KeyOnChild_ReportsPlacementError()
    {
        TransformTestHelpers.Transform("<template v-for=\"i in list\"><div :key=\"i\"></div></template>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVForTemplateKeyPlacement);
    }
}
