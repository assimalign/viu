using System;
using System.Collections.Generic;
using System.Linq;

using Assimalign.Vue.Shared;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/transform.spec.ts: the transform driver
// (node/directive transform ordering, replace/remove, helper registration, third-party injection) and the
// [V01.01.05.02] block-structure (dynamic-children order) invariant.
public class TransformPipelineTests
{
    [Fact]
    public void Transform_SingleRootElement_ProducesElementBlock()
    {
        var result = TransformTestHelpers.Transform("<div></div>");

        var codegen = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        codegen.Tag.ShouldBe("\"div\"");
        codegen.IsBlock.ShouldBeTrue();
        result.ShouldUseHelper("createElementBlock");
        result.ShouldUseHelper("openBlock");
    }

    [Fact]
    public void Transform_MultipleRoots_ProducesStableFragmentBlock()
    {
        var result = TransformTestHelpers.Transform("<div></div><span></span>");

        var codegen = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        codegen.Tag.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("Fragment");
        codegen.ShouldHavePatchFlag(PatchFlags.StableFragment);
        codegen.IsBlock.ShouldBeTrue();
    }

    [Fact]
    public void Transform_Interpolation_RegistersToDisplayStringHelper()
    {
        var result = TransformTestHelpers.Transform("<div>{{ message }}</div>");

        result.ShouldUseHelper("toDisplayString");
    }

    [Fact]
    public void Transform_ThirdPartyNodeTransform_RunsInOrder()
    {
        // transform.spec.ts 'context state' / custom nodeTransforms: user transforms run after the preset.
        var visited = new List<string>();
        var options = TransformOptions.CreateDom();
        options.NodeTransforms = new NodeTransform[]
        {
            (node, _) =>
            {
                if (node is ElementNode element)
                {
                    visited.Add(element.Tag);
                }

                return null;
            },
        };

        TransformTestHelpers.Transform("<div><span></span></div>", options);

        visited.ShouldBe(new[] { "div", "span" });
    }

    [Fact]
    public void Transform_ReplaceNode_SubstitutesInTree()
    {
        // transform.spec.ts 'context.replaceNode': a custom transform swaps a node.
        var options = TransformOptions.CreateDom();
        options.NodeTransforms = new NodeTransform[]
        {
            (node, context) =>
            {
                if (node is ElementNode { Tag: "div" })
                {
                    context.ReplaceNode(new TextNode { Content = "replaced", Location = node.Location });
                }

                return null;
            },
        };

        var result = TransformTestHelpers.Transform("<div></div>", options);

        result.SingleChild().ShouldBeOfType<TextNode>().Content.ShouldBe("replaced");
    }

    [Fact]
    public void Transform_RemoveNode_DropsFromTree()
    {
        // transform.spec.ts 'context.removeNode': a custom transform removes a node.
        var options = TransformOptions.CreateDom();
        options.NodeTransforms = new NodeTransform[]
        {
            (node, context) =>
            {
                if (node is ElementNode { Tag: "span" })
                {
                    context.RemoveNode();
                }

                return null;
            },
        };

        var result = TransformTestHelpers.Transform("<div><span></span><b></b></div>", options);

        var div = result.RootCodegen().ShouldBeOfType<VNodeCall>();
        var children = div.Children.ShouldBeOfType<SyntaxList<TemplateChildNode>>();
        children.Count.ShouldBe(1);
        children[0].ShouldBeOfType<ElementNode>().Tag.ShouldBe("b");
    }

    [Fact]
    public void Transform_DynamicChildrenOrder_IsStableAcrossRuns()
    {
        // [V01.01.05.02] block-structure invariant: the transformed tree (and its dynamic children) is
        // deterministic — equal input yields value-equal output, the incremental-generator caching contract.
        const string template = "<div><span>{{ a }}</span><i v-if=\"ok\"></i><em>{{ b }}</em></div>";

        var first = TransformTestHelpers.Transform(template);
        var second = TransformTestHelpers.Transform(template);

        first.CodegenNode.ShouldBe(second.CodegenNode);
        first.Children.ShouldBe(second.Children);
    }

    [Fact]
    public void Transform_HelperRegistration_DeduplicatesByReference()
    {
        // Two dynamic text children both need toDisplayString but it is registered once.
        var result = TransformTestHelpers.Transform("<div>{{ a }}{{ b }}</div>");

        result.Helpers.Count(helper => helper.Name == "toDisplayString").ShouldBe(1);
    }
}
