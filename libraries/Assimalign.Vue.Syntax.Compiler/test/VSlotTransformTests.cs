using System.Linq;

using Assimalign.Vue.Shared;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/transforms/vSlot.spec.ts: buildSlots, the
// SlotFlags fingerprint (STABLE/DYNAMIC/FORWARDED), and the slot-misuse diagnostics.
public class VSlotTransformTests
{
    private static ObjectExpression Slots(TransformResult result)
    {
        var children = result.CodegenNode.ShouldBeOfType<VNodeCall>().Children;
        return children is CallExpression createSlots
            ? createSlots.Arguments[0].ShouldBeOfType<ObjectExpression>()
            : children.ShouldBeOfType<ObjectExpression>();
    }

    private static string SlotFlag(ObjectExpression slots)
        => slots.Properties.First(p => p.Key is SimpleExpressionNode { Content: "_" }).Value.ShouldBeOfType<SimpleExpressionNode>().Content;

    [Fact]
    public void NamedTemplateSlot_ProducesStableSlotsObject()
    {
        var result = TransformTestHelpers.Transform("<Comp><template #header>title</template></Comp>");

        var slots = Slots(result);
        slots.Property("header").Value.ShouldBeOfType<FunctionExpression>();
        SlotFlag(slots).ShouldBe(((int)SlotFlags.Stable).ToString());
        result.ShouldUseHelper("withCtx");
    }

    [Fact]
    public void OnComponentSlot_ProducesDefaultSlotWithProps()
    {
        var result = TransformTestHelpers.Transform("<Comp v-slot=\"{ item }\">{{ item }}</Comp>");

        var slots = Slots(result);
        var defaultSlot = slots.Property("default").Value.ShouldBeOfType<FunctionExpression>();
        defaultSlot.Parameters.Count.ShouldBe(1);
        defaultSlot.Parameters[0].ShouldBeOfType<SimpleExpressionNode>().Content.ShouldBe("{ item }");
    }

    [Fact]
    public void ConditionalTemplateSlot_ProducesDynamicSlots()
    {
        var result = TransformTestHelpers.Transform("<Comp><template #a v-if=\"ok\">x</template></Comp>");

        var codegenChildren = result.CodegenNode.ShouldBeOfType<VNodeCall>().Children;
        var createSlots = codegenChildren.ShouldBeOfType<CallExpression>();
        createSlots.Callee.ShouldBeOfType<RuntimeHelper>().Name.ShouldBe("createSlots");
        SlotFlag(createSlots.Arguments[0].ShouldBeOfType<ObjectExpression>()).ShouldBe(((int)SlotFlags.Dynamic).ToString());
        result.CodegenNode.ShouldBeOfType<VNodeCall>().ShouldHavePatchFlag(PatchFlags.DynamicSlots);
    }

    [Fact]
    public void ForwardedSlot_IsFlaggedForwarded()
    {
        var result = TransformTestHelpers.Transform("<Comp><slot></slot></Comp>");

        SlotFlag(Slots(result)).ShouldBe(((int)SlotFlags.Forwarded).ToString());
    }

    [Fact]
    public void MixedSlotUsage_ReportsError()
    {
        TransformTestHelpers.Transform("<Comp v-slot=\"x\"><template #header>h</template></Comp>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVSlotMixedSlotUsage);
    }

    [Fact]
    public void DuplicateSlotNames_ReportsError()
    {
        TransformTestHelpers.Transform("<Comp><template #a>1</template><template #a>2</template></Comp>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVSlotDuplicateSlotNames);
    }

    [Fact]
    public void VSlotOnPlainElement_ReportsMisplacedError()
    {
        TransformTestHelpers.Transform("<div v-slot=\"x\"></div>", out var errors);

        errors.ShouldContain(e => e.Code == CompilerErrorCode.XVSlotMisplaced);
    }
}
