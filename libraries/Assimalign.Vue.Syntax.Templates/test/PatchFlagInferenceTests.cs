using Assimalign.Vue.Shared;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

// [V01.01.05.06] patch-flag inference. Ported from vuejs/core
// packages/compiler-core/__tests__/transforms/{transformElement,vBind,vOn}.spec.ts and pinned against the
// vuejs/core v3.5 template-explorer output for each template. Every expected numeric value is the exact
// PatchFlags bit combination @vue/shared emits and the runtime diff consumes; the enum lives once in
// Assimalign.Vue.Shared (see Assimalign.Vue.Shared.Tests.PatchFlagsParityTests for the bit-for-bit table),
// so these tests are the compiler-side half of that shared-constants contract.
public class PatchFlagInferenceTests
{
    private static VNodeCall RootVNode(string source)
        => TransformTestHelpers.Transform(source).RootCodegen().ShouldBeOfType<VNodeCall>();

    // ---- TEXT (1 << 0) ----

    [Fact]
    public void DynamicTextChild_FlagsTextOnly()
    {
        // transformElement.ts: a single interpolation/compound child with NOT_CONSTANT value => TEXT.
        var codegen = RootVNode("<div>{{ message }}</div>");

        codegen.PatchFlag.ShouldBe(PatchFlags.Text);
        ((int)codegen.PatchFlag!.Value).ShouldBe(1);
    }

    [Fact]
    public void StaticTextChild_HasNoPatchFlag()
    {
        // A plain text child can never change, so no flag is emitted (the vnode carries the text directly).
        var codegen = RootVNode("<div>hello</div>");

        codegen.PatchFlag.ShouldBeNull();
    }

    [Fact]
    public void DynamicTextChildWithDynamicClass_CombinesClassAndText()
    {
        // Flags are a bitset: :class => CLASS, {{ x }} => TEXT, combined = 2 | 1 = 3.
        var codegen = RootVNode("<div :class=\"cls\">{{ message }}</div>");

        codegen.ShouldHavePatchFlag(PatchFlags.Class);
        codegen.ShouldHavePatchFlag(PatchFlags.Text);
        ((int)codegen.PatchFlag!.Value).ShouldBe((int)(PatchFlags.Class | PatchFlags.Text));
        ((int)codegen.PatchFlag!.Value).ShouldBe(3);
    }

    // ---- static prop elision ----

    [Fact]
    public void OnlyStaticAttributes_EmitNoPatchFlag()
    {
        // buildProps only runs analyzePatchFlag on directive-produced props; static attributes never flag.
        var codegen = RootVNode("<div class=\"a\" style=\"color:red\" id=\"x\"></div>");

        codegen.PatchFlag.ShouldBeNull();
    }

    // ---- PROPS (1 << 3) and the dynamicProps list ----

    [Fact]
    public void DynamicProps_CollectsExactNamesInSourceOrder()
    {
        // vBind.spec.ts: non-class/style dynamic bindings => PROPS with an ordered dynamicProps name list.
        var codegen = RootVNode("<div :foo=\"a\" :bar=\"b\"></div>");

        codegen.PatchFlag.ShouldBe(PatchFlags.Props);
        codegen.DynamicProps.ShouldBe("[\"foo\", \"bar\"]");
    }

    [Fact]
    public void KeyBinding_IsNeverCollectedAsDynamicProp()
    {
        // analyzePatchFlag skips `key`: the diff handles keys structurally, not as a patched prop.
        var codegen = RootVNode("<div :key=\"k\" :foo=\"a\"></div>");

        codegen.DynamicProps.ShouldBe("[\"foo\"]");
    }

    // ---- FULL_PROPS (1 << 4): replaces CLASS/STYLE/PROPS ----

    [Fact]
    public void DynamicArgument_EscalatesToFullPropsAndReplacesFinerFlags()
    {
        // A dynamic v-bind argument makes the prop keys themselves dynamic; upstream sets FULL_PROPS *instead*
        // of CLASS/STYLE/PROPS (the else-branch in buildProps is skipped entirely when hasDynamicKeys).
        var codegen = RootVNode("<div :[key]=\"v\" :class=\"c\" :style=\"s\" :foo=\"f\"></div>");

        codegen.PatchFlag.ShouldBe(PatchFlags.FullProps);
        ((int)codegen.PatchFlag!.Value).ShouldBe(16);
        (codegen.PatchFlag!.Value & PatchFlags.Class).ShouldBe((PatchFlags)0);
        (codegen.PatchFlag!.Value & PatchFlags.Style).ShouldBe((PatchFlags)0);
        (codegen.PatchFlag!.Value & PatchFlags.Props).ShouldBe((PatchFlags)0);
    }

    [Fact]
    public void ObjectSpreadVBind_EscalatesToFullProps()
    {
        // v-bind="obj" merges an object whose keys are unknown at compile time => FULL_PROPS.
        var codegen = RootVNode("<div v-bind=\"obj\" :class=\"c\"></div>");

        codegen.PatchFlag.ShouldBe(PatchFlags.FullProps);
    }

    // ---- NEED_HYDRATION (1 << 5) ----

    [Fact]
    public void NonClickEventListener_FlagsNeedHydration()
    {
        // transformElement.ts: a non-click, non-reserved event binding needs its listener attached during
        // hydration even when nothing else changes. The handler name is also a dynamic prop (no cacheHandlers),
        // so the flag is PROPS | NEED_HYDRATION = 8 | 32 = 40.
        var codegen = RootVNode("<div @foo=\"bar\"></div>");

        codegen.ShouldHavePatchFlag(PatchFlags.NeedHydration);
        codegen.ShouldHavePatchFlag(PatchFlags.Props);
        ((int)codegen.PatchFlag!.Value).ShouldBe((int)(PatchFlags.Props | PatchFlags.NeedHydration));
        ((int)codegen.PatchFlag!.Value).ShouldBe(40);
        codegen.DynamicProps.ShouldBe("[\"onFoo\"]");
    }

    [Fact]
    public void ClickEventListener_OmitsNeedHydration()
    {
        // onClick is deliberately excluded from NEED_HYDRATION: hydration gives click a dedicated fast path.
        var codegen = RootVNode("<button @click=\"onClick\"></button>");

        (codegen.PatchFlag!.Value & PatchFlags.NeedHydration).ShouldBe((PatchFlags)0);
        codegen.PatchFlag.ShouldBe(PatchFlags.Props);
        codegen.DynamicProps.ShouldBe("[\"onClick\"]");
    }

    // ---- NEED_PATCH (1 << 9) ----

    [Fact]
    public void TemplateReferenceOnly_FlagsNeedPatch()
    {
        // A ref with no dynamic props still needs runtime work (assigning the ref), so NEED_PATCH is emitted.
        var codegen = RootVNode("<div ref=\"root\"></div>");

        codegen.PatchFlag.ShouldBe(PatchFlags.NeedPatch);
        ((int)codegen.PatchFlag!.Value).ShouldBe(512);
    }

    [Fact]
    public void RuntimeCustomDirectiveOnly_FlagsNeedPatchAndResolvesDirective()
    {
        // A user directive with no other dynamic bindings => NEED_PATCH, plus a resolveDirective registration.
        var result = TransformTestHelpers.Transform("<div v-custom=\"x\"></div>");
        var codegen = result.RootCodegen().ShouldBeOfType<VNodeCall>();

        codegen.PatchFlag.ShouldBe(PatchFlags.NeedPatch);
        result.ShouldUseHelper("resolveDirective");
        result.ShouldUseHelper("withDirectives");
        result.Directives.ShouldContain("custom");
    }

    [Fact]
    public void NeedPatchIsSuppressed_WhenElementAlreadyHasDynamicProps()
    {
        // NEED_PATCH only fills the gap when patchFlag is otherwise 0 (or NEED_HYDRATION): a ref alongside a
        // dynamic prop rides the PROPS diff instead of adding NEED_PATCH.
        var codegen = RootVNode("<div ref=\"root\" :foo=\"a\"></div>");

        codegen.PatchFlag.ShouldBe(PatchFlags.Props);
        (codegen.PatchFlag!.Value & PatchFlags.NeedPatch).ShouldBe((PatchFlags)0);
    }

    // ---- component class/style as dynamic props ----

    [Fact]
    public void Component_TreatsDynamicClassAndStyleAsRegularProps()
    {
        // For a component, class/style are ordinary props (not element attributes), so they escalate PROPS and
        // land in dynamicProps rather than setting the CLASS/STYLE bits.
        var codegen = RootVNode("<Comp :class=\"c\" :style=\"s\" :foo=\"f\"></Comp>");

        codegen.IsComponent.ShouldBeTrue();
        codegen.PatchFlag.ShouldBe(PatchFlags.Props);
        (codegen.PatchFlag!.Value & PatchFlags.Class).ShouldBe((PatchFlags)0);
        (codegen.PatchFlag!.Value & PatchFlags.Style).ShouldBe((PatchFlags)0);
        codegen.DynamicProps.ShouldBe("[\"class\", \"style\", \"foo\"]");
    }

    // ---- DYNAMIC_SLOTS (1 << 10) ----

    [Fact]
    public void DynamicSlotName_FlagsDynamicSlots()
    {
        // buildSlots: a non-static slot name forces the component's slots dynamic (DYNAMIC_SLOTS).
        var codegen = RootVNode("<Comp><template #[name]>x</template></Comp>");

        codegen.ShouldHavePatchFlag(PatchFlags.DynamicSlots);
    }

    // ---- numeric parity centrepiece ----

    // Each row is the exact patchFlag the vuejs/core v3.5 template-explorer stamps for the template (base
    // compiler options: no prefixIdentifiers, no cacheHandlers). This is the compiler-side mirror of
    // Assimalign.Vue.Shared.Tests.PatchFlagsParityTests — same numbers, produced by inference rather than by
    // reading the enum, so a divergence in either the enum or the inference is caught.
    [Theory]
    [InlineData("<div>{{ x }}</div>", 1)]                    // TEXT
    [InlineData("<div :class=\"c\"></div>", 2)]              // CLASS
    [InlineData("<div :style=\"s\"></div>", 4)]              // STYLE
    [InlineData("<div :foo=\"f\"></div>", 8)]                // PROPS
    [InlineData("<div :[k]=\"v\"></div>", 16)]               // FULL_PROPS
    [InlineData("<div @foo=\"bar\"></div>", 40)]             // PROPS | NEED_HYDRATION
    [InlineData("<div ref=\"r\"></div>", 512)]               // NEED_PATCH
    [InlineData("<div :class=\"c\">{{ x }}</div>", 3)]       // CLASS | TEXT
    public void EmittedPatchFlag_IsBitIdenticalToUpstream(string template, int expected)
    {
        var codegen = RootVNode(template);

        ((int)codegen.PatchFlag!.Value).ShouldBe(expected);
    }

    // ---- caching contract: equal input yields value-equal flags ----

    [Fact]
    public void PatchFlagInference_IsDeterministic()
    {
        // The incremental-generator caching contract: identical templates yield value-equal codegen (flags,
        // dynamicProps, and all).
        const string template = "<div :class=\"c\" :foo=\"a\" @bar=\"h\">{{ x }}</div>";

        var first = RootVNode(template);
        var second = RootVNode(template);

        first.ShouldBe(second);
        first.PatchFlag.ShouldBe(second.PatchFlag);
        first.DynamicProps.ShouldBe(second.DynamicProps);
    }
}
