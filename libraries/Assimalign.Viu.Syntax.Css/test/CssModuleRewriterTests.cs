using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Pins the CSS Modules class-name rewrite (<see cref="CssModuleRewriter"/>, [V01.01.06.06]) against the
/// behavior of Vue 3.5's <c>@style module</c> compilation (<c>@vue/compiler-sfc</c> <c>compileStyle()</c>
/// modules mode, https://vuejs.org/api/sfc-css-features.html#css-modules): local class selectors are
/// renamed to deterministic, component-scoped hashed names, the original → hashed map is returned for the
/// generated <c>$style</c> accessor, and the rewrite composes with <c>scoped</c>.
/// </summary>
public sealed class CssModuleRewriterTests
{
    private const string Salt = "abc12345";

    // The documented scheme: "<original>_<8 lowercase hex>".
    private static readonly Regex HashedName = new(@"^[A-Za-z0-9_-]+_[0-9a-f]{8}$", RegexOptions.Compiled);

    [Fact]
    public void Rewrite_RenamesClassSelector_AndRecordsTheMap()
    {
        var result = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".box { color: red; }"), Salt);

        result.Classes.ShouldContainKey("box");
        var hashed = result.Classes["box"];
        HashedName.IsMatch(hashed).ShouldBeTrue();
        hashed.ShouldStartWith("box_");
        // The rewritten tree carries the hashed selector text.
        CssStylesheetWriter.Write(result.Stylesheet).ShouldContain("." + hashed + " {");
        CssStylesheetWriter.Write(result.Stylesheet).ShouldNotContain(".box {");
    }

    [Fact]
    public void Rewrite_IsDeterministic_ForTheSameInputAndSalt()
    {
        var first = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".box { color: red; }"), Salt);
        var second = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".box { color: red; }"), Salt);

        // Deterministic hashing is the asset-caching contract; the rewritten trees are value-equal.
        second.Classes["box"].ShouldBe(first.Classes["box"]);
        second.Stylesheet.ShouldBe(first.Stylesheet);
    }

    [Fact]
    public void Rewrite_HashesDifferByComponentSalt()
    {
        var a = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".box { color: red; }"), "aaaa0000");
        var b = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".box { color: red; }"), "bbbb1111");

        // Unique per component: the same class in two components hashes differently.
        a.Classes["box"].ShouldNotBe(b.Classes["box"]);
    }

    [Fact]
    public void Rewrite_RenamesEveryClassInACompound_KeepingTypeAndId()
    {
        var result = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet("div.a.b#c { color: red; }"), Salt);

        result.Classes.ShouldContainKey("a");
        result.Classes.ShouldContainKey("b");
        var css = CssStylesheetWriter.Write(result.Stylesheet);
        css.ShouldContain("div." + result.Classes["a"] + "." + result.Classes["b"] + "#c {");
        // Type and id selectors are untouched — only classes are module-local.
        result.Classes.ShouldNotContainKey("c");
    }

    [Fact]
    public void Rewrite_ReusesOneHash_ForARepeatedClass()
    {
        var result = CssModuleRewriter.Rewrite(
            CssTestHelpers.ParseStylesheet(".box { color: red; } .box:hover { color: blue; }"),
            Salt);

        // A class used more than once maps to a single hashed name (the one-name-one-hash module contract).
        result.Classes.Count.ShouldBe(1);
        var css = CssStylesheetWriter.Write(result.Stylesheet);
        Regex.Matches(css, Regex.Escape("." + result.Classes["box"])).Count.ShouldBe(2);
    }

    [Fact]
    public void Rewrite_RenamesClassesNestedInConditionalGroups()
    {
        var result = CssModuleRewriter.Rewrite(
            CssTestHelpers.ParseStylesheet("@media (min-width: 100px) { .box { color: red; } }"),
            Salt);

        result.Classes.ShouldContainKey("box");
        CssStylesheetWriter.Write(result.Stylesheet).ShouldContain("." + result.Classes["box"] + " {");
    }

    [Fact]
    public void Rewrite_LeavesClassesInsideFunctionalPseudosVerbatim()
    {
        // :not(.inner) keeps its argument verbatim (parser non-goal), so .inner is not module-renamed —
        // only .outer is. Documented in CssModuleRewriter's remarks.
        var result = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".outer:not(.inner) { color: red; }"), Salt);

        result.Classes.ShouldContainKey("outer");
        result.Classes.ShouldNotContainKey("inner");
        CssStylesheetWriter.Write(result.Stylesheet).ShouldContain(":not(.inner)");
    }

    [Fact]
    public void Rewrite_ComposesWithScoped()
    {
        // The compiler runs the module rename over the tree, then the scoped serializer — both read the
        // parsed selector parts, so the class is renamed AND the [data-v] attribute lands on it.
        var module = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".box { color: red; }"), Salt);

        var scoped = CssScopedRewriter.Rewrite(module.Stylesheet, "data-v-test");

        scoped.ShouldBe("." + module.Classes["box"] + "[data-v-test] {\n  color: red;\n}\n");
    }

    [Fact]
    public void Rewrite_LeavesNonClassOnlyStylesheetUnchanged()
    {
        var result = CssModuleRewriter.Rewrite(CssTestHelpers.ParseStylesheet("div { color: red; }"), Salt);

        result.Classes.ShouldBeEmpty();
        CssStylesheetWriter.Write(result.Stylesheet).ShouldBe("div {\n  color: red;\n}\n");
    }
}
