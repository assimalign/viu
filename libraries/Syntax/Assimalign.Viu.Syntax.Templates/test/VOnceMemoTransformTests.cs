using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// These cases ARE the contract for v-once (caches the subtree) and v-memo (wraps it in withMemo),
// including their interaction when both appear on a v-for element.
public class VOnceMemoTransformTests
{
    [Fact]
    public void VOnce_CachesSubtreeAndRegistersSetBlockTracking()
    {
        var result = TransformTestHelpers.Transform("<div v-once></div>");

        var cache = result.CodegenNode.ShouldBeOfType<CacheExpression>();
        cache.InVOnce.ShouldBeTrue();
        cache.NeedPauseTracking.ShouldBeTrue();
        cache.Value.ShouldBeOfType<VirtualNodeCall>();
        result.ShouldUseHelper("setBlockTracking");
        result.Cached.Count.ShouldBe(1);
    }

    [Fact]
    public void VMemo_WrapsSubtreeInWithMemo()
    {
        var result = TransformTestHelpers.Transform("<div v-memo=\"[msg]\"></div>");

        var memo = result.CodegenNode.ShouldBeOfType<CallExpression>();
        memo.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("withMemo");
        memo.Arguments[0].ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("[msg]");
        var factory = memo.Arguments[1].ShouldBeOfType<FunctionExpression>();
        factory.Returns.ShouldBeOfType<VirtualNodeCall>().IsBlock.ShouldBeTrue();
        memo.Arguments[2].ShouldBe("_cache");
        memo.Arguments[3].ShouldBe("0");
        result.Cached.Count.ShouldBe(1);
    }

    [Fact]
    public void VMemo_WithVFor_ProducesPerItemMemoLoop()
    {
        // vMemo.spec.ts 'on v-for': v-for's renderList iterator memoizes each item (no separate withMemo).
        var result = TransformTestHelpers.Transform("<div v-for=\"item in list\" v-memo=\"[item]\"></div>");

        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        var codegen = result.GetCodegenNode(forNode).ShouldBeOfType<VirtualNodeCall>();
        var renderList = codegen.Children.ShouldBeOfType<CallExpression>();
        var loop = renderList.Arguments[1].ShouldBeOfType<FunctionExpression>();
        // the loop takes the aliases plus a trailing _cached parameter and has a block body
        loop.Parameters[loop.Parameters.Count - 1].ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("_cached");
        loop.Body.ShouldNotBeNull();
        result.ShouldUseHelper("isMemoSame");
        result.Cached.Count.ShouldBe(1);
    }
}
