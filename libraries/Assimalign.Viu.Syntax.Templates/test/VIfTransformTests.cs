using System.Linq;

using Assimalign.Viu.Shared;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Ported from vuejs/core packages/compiler-core/__tests__/transforms/vIf.spec.ts: branch grouping into a
// single IfNode, the conditional-expression codegen, stable per-branch keys, and the diagnostics.
public class VIfTransformTests
{
    [Fact]
    public void VIf_Single_ProducesIfNodeWithOneBranch()
    {
        var result = TransformTestHelpers.Transform("<div v-if=\"ok\"></div>");

        var ifNode = result.SingleChild().ShouldBeOfType<IfNode>();
        ifNode.Branches.Count.ShouldBe(1);
        ifNode.Branches[0].Condition.ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("ok");
    }

    [Fact]
    public void VIf_Single_CompilesToConditionalWithCommentAlternate()
    {
        var result = TransformTestHelpers.Transform("<div v-if=\"ok\"></div>");
        var ifNode = result.SingleChild().ShouldBeOfType<IfNode>();

        var conditional = result.GetCodegenNode(ifNode).ShouldBeOfType<ConditionalExpression>();
        conditional.Test.StaticContent().ShouldBe("ok");
        var consequent = conditional.Consequent.ShouldBeOfType<VNodeCall>();
        consequent.IsBlock.ShouldBeTrue();
        consequent.Props.ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("0");
        conditional.Alternate.ShouldBeOfType<CallExpression>().Callee
            .ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("createCommentVNode");
    }

    [Fact]
    public void VIfElse_GroupsIntoSingleIfNodeWithTwoBranches()
    {
        var result = TransformTestHelpers.Transform("<div v-if=\"a\"></div><p v-else></p>");

        result.Children.Count.ShouldBe(1);
        var ifNode = result.Children[0].ShouldBeOfType<IfNode>();
        ifNode.Branches.Count.ShouldBe(2);
        ifNode.Branches[0].Condition.ShouldNotBeNull();
        ifNode.Branches[1].Condition.ShouldBeNull();
    }

    [Fact]
    public void VIfElse_CompilesToConditionalWithBranchKeys()
    {
        var result = TransformTestHelpers.Transform("<div v-if=\"a\"></div><p v-else></p>");
        var ifNode = result.Children[0].ShouldBeOfType<IfNode>();

        var conditional = result.GetCodegenNode(ifNode).ShouldBeOfType<ConditionalExpression>();
        conditional.Consequent.ShouldBeOfType<VNodeCall>().Props
            .ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("0");
        // else branch has no condition, so the alternate is its block directly (key 1).
        var alternate = conditional.Alternate.ShouldBeOfType<VNodeCall>();
        alternate.Tag.ShouldBe("\"p\"");
        alternate.Props.ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("1");
    }

    [Fact]
    public void VIfElseIf_ChainsConditionalsWithIncrementingKeys()
    {
        var result = TransformTestHelpers.Transform("<div v-if=\"a\"></div><p v-else-if=\"b\"></p><i v-else></i>");
        var ifNode = result.Children[0].ShouldBeOfType<IfNode>();
        ifNode.Branches.Count.ShouldBe(3);

        var conditional = result.GetCodegenNode(ifNode).ShouldBeOfType<ConditionalExpression>();
        conditional.Test.StaticContent().ShouldBe("a");
        var nested = conditional.Alternate.ShouldBeOfType<ConditionalExpression>();
        nested.Test.StaticContent().ShouldBe("b");
        nested.Consequent.ShouldBeOfType<VNodeCall>().Props
            .ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("1");
        nested.Alternate.ShouldBeOfType<VNodeCall>().Props
            .ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("2");
    }

    [Fact]
    public void TemplateVIf_WithMultipleChildren_CompilesToKeyedFragment()
    {
        var result = TransformTestHelpers.Transform("<template v-if=\"ok\"><div></div><p></p></template>");
        var ifNode = result.SingleChild().ShouldBeOfType<IfNode>();
        ifNode.Branches[0].IsTemplateIf.ShouldBeTrue();
        ifNode.Branches[0].Children.Count.ShouldBe(2);

        var conditional = result.GetCodegenNode(ifNode).ShouldBeOfType<ConditionalExpression>();
        var fragment = conditional.Consequent.ShouldBeOfType<VNodeCall>();
        fragment.Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
        fragment.ShouldHavePatchFlag(PatchFlags.StableFragment);
        fragment.Props.ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("0");
    }

    [Fact]
    public void VIf_NoExpression_ReportsError()
    {
        TransformTestHelpers.Transform("<div v-if></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVIfNoExpression);
    }

    [Fact]
    public void VElse_NoAdjacentIf_ReportsError()
    {
        TransformTestHelpers.Transform("<div></div><p v-else></p>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVElseNoAdjacentIf);
    }

    [Fact]
    public void VIf_SameKeyOnBranches_ReportsError()
    {
        TransformTestHelpers.Transform("<div v-if=\"a\" key=\"x\"></div><p v-else key=\"x\"></p>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVIfSameKey);
    }

    [Fact]
    public void VIf_CommentBetweenBranches_IsAbsorbed()
    {
        // vIf.spec.ts 'comment between branches': the comment is moved into the branch, not left dangling.
        var result = TransformTestHelpers.Transform("<div v-if=\"a\"></div><!--c--><p v-else></p>");

        result.Children.Count.ShouldBe(1);
        result.Children[0].ShouldBeOfType<IfNode>().Branches.Count.ShouldBe(2);
    }
}
