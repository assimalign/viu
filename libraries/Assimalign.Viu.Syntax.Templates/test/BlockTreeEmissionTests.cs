using Assimalign.Viu.Shared;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// [V01.01.05.06] block-tree emission. Ported from vuejs/core
// packages/compiler-core/__tests__/transforms/{vFor,vIf}.spec.ts and the "tree flattening" section of
// https://vuejs.org/guide/extras/rendering-mechanism.html. Pins which vnodes open an optimization block
// (openBlock + createElementBlock/createBlock), how v-if branches and v-for fragments become blocks, and
// where block tracking is disabled — the compiler half of the compiler-informed VDOM contract.
public class BlockTreeEmissionTests
{
    // ---- the template root opens a block ----

    [Fact]
    public void SingleRootElement_OpensBlockWithTrackingEnabled()
    {
        var result = TransformTestHelpers.Transform("<div></div>");
        var codegen = result.RootCodegen().ShouldBeOfType<VNodeCall>();

        codegen.IsBlock.ShouldBeTrue();
        codegen.DisableTracking.ShouldBeFalse();
        result.ShouldUseHelper("openBlock");
        result.ShouldUseHelper("createElementBlock");
    }

    [Fact]
    public void SingleRootComponent_OpensBlockViaCreateBlock()
    {
        var result = TransformTestHelpers.Transform("<Comp></Comp>");
        var codegen = result.RootCodegen().ShouldBeOfType<VNodeCall>();

        codegen.IsBlock.ShouldBeTrue();
        codegen.IsComponent.ShouldBeTrue();
        result.ShouldUseHelper("openBlock");
        result.ShouldUseHelper("createBlock");
    }

    // ---- nested elements do NOT open blocks; they are collected by the enclosing block at runtime ----

    [Fact]
    public void NestedDynamicElement_DoesNotOpenBlockButKeepsItsPatchFlag()
    {
        // Only block roots open blocks; a nested dynamic element stays a plain (element) vnode carrying its
        // patch flag, to be collected into the parent block's dynamicChildren at runtime.
        var result = TransformTestHelpers.Transform("<div><span :id=\"x\"></span></div>");
        var root = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        var children = root.Children.ShouldBeOfType<SyntaxList<TemplateChildNode>>();

        var span = result.GetCodegenNode(children[0]).ShouldBeOfType<VNodeCall>();
        span.IsBlock.ShouldBeFalse();
        span.ShouldHavePatchFlag(PatchFlags.Props);
        span.DynamicProps.ShouldBe("[\"id\"]");
    }

    [Fact]
    public void FullyStaticSubtree_HasNeitherBlockNorFlagBelowTheRoot()
    {
        var result = TransformTestHelpers.Transform("<div><span></span></div>");
        var root = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        var children = root.Children.ShouldBeOfType<SyntaxList<TemplateChildNode>>();

        var span = result.GetCodegenNode(children[0]).ShouldBeOfType<VNodeCall>();
        span.IsBlock.ShouldBeFalse();
        span.PatchFlag.ShouldBeNull();
    }

    // ---- svg/foreignObject open a block boundary even when nested ----

    [Fact]
    public void NestedSvg_OpensItsOwnBlockBoundary()
    {
        // transformElement.ts forces a block for svg/foreignObject/math because their namespace boundary breaks
        // the flat dynamicChildren assumption of the enclosing block.
        var result = TransformTestHelpers.Transform("<div><svg></svg></div>");
        var root = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        var children = root.Children.ShouldBeOfType<SyntaxList<TemplateChildNode>>();

        var svg = result.GetCodegenNode(children[0]).ShouldBeOfType<VNodeCall>();
        svg.IsBlock.ShouldBeTrue();
    }

    // ---- multi-root templates emit a stable fragment block ----

    [Fact]
    public void MultiRoot_EmitsStableFragmentBlock()
    {
        var result = TransformTestHelpers.Transform("<div></div><span></span>");
        var codegen = result.RootCodegen().ShouldBeOfType<VNodeCall>();

        codegen.Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
        codegen.IsBlock.ShouldBeTrue();
        codegen.DisableTracking.ShouldBeFalse();
        codegen.ShouldHavePatchFlag(PatchFlags.StableFragment);
    }

    // ---- v-if: each branch is its own block with a stable synthetic key ----

    [Fact]
    public void VIfBranch_OpensBlockWithTrackingEnabled()
    {
        var result = TransformTestHelpers.Transform("<div v-if=\"ok\"></div>");
        var ifNode = result.SingleChild().ShouldBeOfType<IfNode>();

        var conditional = result.GetCodegenNode(ifNode).ShouldBeOfType<ConditionalExpression>();
        var branchBlock = conditional.Consequent.ShouldBeOfType<VNodeCall>();
        branchBlock.IsBlock.ShouldBeTrue();
        branchBlock.DisableTracking.ShouldBeFalse();
        branchBlock.Props.ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("0");
    }

    // ---- v-for: a fragment block whose tracking is disabled when the source is dynamic ----

    [Fact]
    public void VForFragment_OpensBlockAndDisablesTracking()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"i in list\">{{ i }}</div>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        var fragment = result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>();

        fragment.Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
        fragment.IsBlock.ShouldBeTrue();
        // A v-for's item count and identity can change, so the fragment does not collect a stable child list.
        fragment.DisableTracking.ShouldBeTrue();
        fragment.ShouldHavePatchFlag(PatchFlags.UnkeyedFragment);
    }

    [Fact]
    public void VForLoopItem_BecomesABlockWhenSourceIsDynamic()
    {
        // vFor.spec.ts: the per-item vnode is forced into a block so each item roots its own dynamicChildren.
        var result = TransformTestHelpers.Transform("<div v-for=\"i in list\">{{ i }}</div>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        var fragment = result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>();

        var renderList = fragment.Children.ShouldBeOfType<CallExpression>();
        var iterator = renderList.Arguments[1].ShouldBeOfType<FunctionExpression>();
        var itemBlock = iterator.Returns.ShouldBeOfType<VNodeCall>();
        itemBlock.Tag.ShouldBe("\"div\"");
        itemBlock.IsBlock.ShouldBeTrue();
        itemBlock.ShouldHavePatchFlag(PatchFlags.Text);
    }

    [Fact]
    public void KeyedVFor_FlagsKeyedFragment()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"i in list\" :key=\"i.id\"></div>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();

        result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>().ShouldHavePatchFlag(PatchFlags.KeyedFragment);
    }

    [Fact]
    public void TemplateVFor_InnerWrapper_IsAStableFragment()
    {
        // The outer renderList fragment is unkeyed/dynamic, but each iteration's multi-child wrapper has a
        // fixed child order => STABLE_FRAGMENT.
        var result = TransformTestHelpers.Transform("<template v-for=\"i in list\"><div></div><p></p></template>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        var fragment = result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>();

        fragment.ShouldHavePatchFlag(PatchFlags.UnkeyedFragment);
        var iterator = fragment.Children.ShouldBeOfType<CallExpression>().Arguments[1].ShouldBeOfType<FunctionExpression>();
        var wrapper = iterator.Returns.ShouldBeOfType<VNodeCall>();
        wrapper.Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
        wrapper.IsBlock.ShouldBeTrue();
        wrapper.ShouldHavePatchFlag(PatchFlags.StableFragment);
    }

    // ---- v-if / v-for interplay ----

    [Fact]
    public void VForContainingVIf_NestsAConditionalInsideTheItemBlock()
    {
        var result = TransformTestHelpers.Transform("<div v-for=\"i in list\"><span v-if=\"i.ok\"></span></div>");
        var forNode = result.SingleChild().ShouldBeOfType<ForNode>();
        var fragment = result.GetCodegenNode(forNode).ShouldBeOfType<VNodeCall>();

        fragment.ShouldHavePatchFlag(PatchFlags.UnkeyedFragment);
        fragment.DisableTracking.ShouldBeTrue();

        var iterator = fragment.Children.ShouldBeOfType<CallExpression>().Arguments[1].ShouldBeOfType<FunctionExpression>();
        var itemBlock = iterator.Returns.ShouldBeOfType<VNodeCall>();
        itemBlock.Tag.ShouldBe("\"div\"");
        itemBlock.IsBlock.ShouldBeTrue();

        // The item's child is the v-if, folded into an IfNode whose codegen is the branch conditional.
        var itemChildren = itemBlock.Children.ShouldBeOfType<SyntaxList<TemplateChildNode>>();
        var innerIf = itemChildren[0].ShouldBeOfType<IfNode>();
        var conditional = result.GetCodegenNode(innerIf).ShouldBeOfType<ConditionalExpression>();
        conditional.Test.StaticContent().ShouldBe("i.ok");
        conditional.Consequent.ShouldBeOfType<VNodeCall>().IsBlock.ShouldBeTrue();
    }

    [Fact]
    public void VIfBranchContainingVFor_UsesTheForFragmentAsTheBranchBlock()
    {
        // When a branch's only child is a v-for, upstream injects the branch key into the v-for fragment
        // instead of adding a wrapper — the fragment itself becomes the branch block.
        var result = TransformTestHelpers.Transform("<template v-if=\"ok\"><div v-for=\"i in list\"></div></template>");
        var ifNode = result.SingleChild().ShouldBeOfType<IfNode>();

        var conditional = result.GetCodegenNode(ifNode).ShouldBeOfType<ConditionalExpression>();
        var branchBlock = conditional.Consequent.ShouldBeOfType<VNodeCall>();
        branchBlock.Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
        branchBlock.IsBlock.ShouldBeTrue();
        branchBlock.DisableTracking.ShouldBeTrue();
        branchBlock.ShouldHavePatchFlag(PatchFlags.UnkeyedFragment);
        branchBlock.Props.ShouldBeOfType<ObjectExpression>().Property("key").Value.StaticContent().ShouldBe("0");
    }
}
