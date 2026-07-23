using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Css;

// The scoped-CSS rewrite ([V01.01.06.04]), pinned against @vue/compiler-sfc's pluginScoped test
// expectations (packages/compiler-sfc/__tests__/compileStyle.spec.ts) so a divergence from Vue's
// documented scoping semantics is caught, not enshrined. Scope id "data-v-test" yields the attribute
// "[data-v-test]" (no value) and the keyframes short id "test", matching upstream's fixtures.
// See https://vuejs.org/api/sfc-css-features.html.
public class CssScopedRewriterTests
{
    [Fact]
    public void Rewrite_SimpleSelector_AppendsAttributeToLastCompound()
        => CssTestHelpers.Scope(".foo { color: red; }").ShouldContain(".foo[data-v-test] {");

    [Fact]
    public void Rewrite_DescendantSelector_ScopesOnlyLastCompound()
        => CssTestHelpers.Scope("h1 .foo { color: red; }").ShouldContain("h1 .foo[data-v-test] {");

    [Fact]
    public void Rewrite_ChildCombinator_ScopesLastCompound()
        => CssTestHelpers.Scope("h1 > .foo { color: red; }").ShouldContain("h1 > .foo[data-v-test] {");

    [Fact]
    public void Rewrite_GroupedSelectors_ScopeEachIndependently()
        => CssTestHelpers.Scope("h1 .foo, .bar, .baz { color: red; }")
            .ShouldContain("h1 .foo[data-v-test], .bar[data-v-test], .baz[data-v-test] {");

    [Fact]
    public void Rewrite_PseudoClass_KeepsAttributeBeforePseudo()
        => CssTestHelpers.Scope(".foo:after { color: red; }").ShouldContain(".foo[data-v-test]:after {");

    [Fact]
    public void Rewrite_LeadingPseudoElement_PrependsAttribute()
        => CssTestHelpers.Scope("::selection { color: red; }").ShouldContain("[data-v-test]::selection {");

    [Fact]
    public void Rewrite_DeepFunction_StopsScopingAtInnerSelector()
        => CssTestHelpers.Scope(":deep(.foo) { color: red; }").ShouldContain("[data-v-test] .foo {");

    [Fact]
    public void Rewrite_DeepAfterCompound_ScopesTheCompoundThenDeepDescendant()
        => CssTestHelpers.Scope(".a :deep(.foo) { color: red; }").ShouldContain(".a[data-v-test] .foo {");

    [Fact]
    public void Rewrite_VDeepAlias_BehavesLikeDeep()
        => CssTestHelpers.Scope("::v-deep(.foo) { color: red; }").ShouldContain("[data-v-test] .foo {");

    [Fact]
    public void Rewrite_Slotted_UsesSlottedAttributeSuffix()
        => CssTestHelpers.Scope(":slotted(.foo) { color: red; }").ShouldContain(".foo[data-v-test-s] {");

    [Fact]
    public void Rewrite_VSlottedAlias_ScopesLastCompoundWithSuffix()
        => CssTestHelpers.Scope("::v-slotted(.foo .bar) { color: red; }").ShouldContain(".foo .bar[data-v-test-s] {");

    [Fact]
    public void Rewrite_Global_LeavesInnerSelectorUnscoped()
    {
        var output = CssTestHelpers.Scope(":global(.foo) { color: red; }");
        output.ShouldContain(".foo {");
        output.ShouldNotContain("data-v-test");
    }

    [Fact]
    public void Rewrite_GlobalAfterCompounds_ReplacesWholeSelector()
    {
        var output = CssTestHelpers.Scope(".baz .qux ::v-global(.foo .bar) { color: red; }");
        output.ShouldContain(".foo .bar {");
        output.ShouldNotContain("data-v-test");
        output.ShouldNotContain(".baz");
    }

    [Fact]
    public void Rewrite_LeadingUniversal_IsDropped()
    {
        CssTestHelpers.Scope("* { color: red; }").ShouldContain("[data-v-test] {");
        CssTestHelpers.Scope("* .foo { color: red; }").ShouldContain(".foo[data-v-test] {");
    }

    [Fact]
    public void Rewrite_TrailingUniversal_KeepsAttributeOnPrecedingCompound()
        => CssTestHelpers.Scope(".foo * { color: red; }").ShouldContain(".foo[data-v-test] * {");

    [Fact]
    public void Rewrite_Keyframes_SuffixesNameAndAnimationReferences()
    {
        var source =
            "@keyframes color {\n" +
            "  from { color: red; }\n" +
            "  to { color: green; }\n" +
            "}\n" +
            ".anim { animation: color 5s infinite; animation-name: color; }\n";

        var output = CssTestHelpers.Scope(source);

        output.ShouldContain("@keyframes color-test {");
        output.ShouldContain("animation: color-test 5s infinite;");
        output.ShouldContain("animation-name: color-test;");
    }

    [Fact]
    public void Rewrite_Keyframes_ForwardReferenceFromEarlierRule_IsRewritten()
    {
        // The animation declaration precedes its @keyframes; the two-pass collect resolves it regardless
        // of source order (upstream collects in AtRule, rewrites in OnceExit).
        var source =
            ".anim { animation-name: spin; }\n" +
            "@keyframes spin { from { opacity: 0; } to { opacity: 1; } }\n";

        var output = CssTestHelpers.Scope(source);

        output.ShouldContain("animation-name: spin-test;");
        output.ShouldContain("@keyframes spin-test {");
    }

    [Fact]
    public void Rewrite_NestedMediaQuery_ScopesInnerRules()
    {
        var output = CssTestHelpers.Scope("@media (max-width: 600px) { .a .b { color: red; } }");

        output.ShouldContain("@media (max-width: 600px) {");
        output.ShouldContain(".a .b[data-v-test] {");
    }

    [Fact]
    public void Rewrite_PreservesImportant()
        => CssTestHelpers.Scope(".a { color: red !important; }").ShouldContain("color: red !important;");

    [Fact]
    public void Rewrite_IsDeterministic_AcrossRepeatedRuns()
    {
        var source = "h1 .foo, .bar { color: red; } @keyframes spin { to { opacity: 1; } }";
        var stylesheet = CssTestHelpers.ParseStylesheet(source);

        var first = CssScopedRewriter.Rewrite(stylesheet, "data-v-abc123");
        var second = CssScopedRewriter.Rewrite(stylesheet, "data-v-abc123");

        second.ShouldBe(first);
    }

    [Fact]
    public void Rewrite_FullOutput_IsCanonicalDeterministicText()
    {
        // A whole-output snapshot pins the deterministic serialization (two-space indent, "prop: value;",
        // "selector {" bracing) alongside the scoping.
        const string expected =
            ".foo[data-v-test] {\n" +
            "  color: red;\n" +
            "}\n";

        CssTestHelpers.Scope(".foo{color:red}").ShouldBe(expected);
    }
}
