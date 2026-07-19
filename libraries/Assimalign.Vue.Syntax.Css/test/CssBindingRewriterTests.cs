using System.Linq;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Pins the <c>v-bind()</c>-in-CSS rewrite (<see cref="CssBindingRewriter"/>, [V01.01.06.06]) against the
/// behavior of Vue 3.5's <c>cssVars.ts</c> (<c>@vue/compiler-sfc</c>,
/// https://vuejs.org/api/sfc-css-features.html#v-bind-in-css): each <c>v-bind(expr)</c> becomes a
/// component-scoped <c>var(--&lt;hash&gt;)</c>, expressions are collected (and de-duplicated) for the
/// runtime, malformed usages surface recoverable diagnostics, and the rewrite composes with <c>scoped</c>.
/// </summary>
public sealed class CssBindingRewriterTests
{
    private const string Salt = "abc12345";

    private static readonly Regex Hash = new(@"^[0-9a-f]{8}$", RegexOptions.Compiled);

    [Fact]
    public void Rewrite_ReplacesBinding_AndRecordsTheExpression()
    {
        var result = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind(color); }"), Salt);

        result.Diagnostics.ShouldBeEmpty();
        var binding = result.Bindings.ShouldHaveSingleItem();
        binding.Expression.ShouldBe("color");
        Hash.IsMatch(binding.Name).ShouldBeTrue();
        CssStylesheetWriter.Write(result.Stylesheet).ShouldContain("color: var(--" + binding.Name + ");");
    }

    [Fact]
    public void Rewrite_TrimsWhitespaceAndStripsQuotes()
    {
        var spaced = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind( color ); }"), Salt);
        var quoted = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind('theme.primary'); }"), Salt);

        spaced.Bindings.ShouldHaveSingleItem().Expression.ShouldBe("color");
        quoted.Bindings.ShouldHaveSingleItem().Expression.ShouldBe("theme.primary");
    }

    [Fact]
    public void Rewrite_HandlesBindingNestedInAFunction()
    {
        var result = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { width: calc(v-bind(size) + 2px); }"), Salt);

        var binding = result.Bindings.ShouldHaveSingleItem();
        binding.Expression.ShouldBe("size");
        CssStylesheetWriter.Write(result.Stylesheet).ShouldContain("width: calc(var(--" + binding.Name + ") + 2px);");
    }

    [Fact]
    public void Rewrite_DeduplicatesRepeatedExpression()
    {
        var result = CssBindingRewriter.Rewrite(
            CssTestHelpers.ParseStylesheet(".a { color: v-bind(c); } .b { background: v-bind(c); }"),
            Salt);

        // A repeated expression yields one binding and one custom property (upstream vars.includes dedupe).
        var binding = result.Bindings.ShouldHaveSingleItem();
        Regex.Matches(CssStylesheetWriter.Write(result.Stylesheet), Regex.Escape("var(--" + binding.Name + ")")).Count.ShouldBe(2);
    }

    [Fact]
    public void Rewrite_CollectsDistinctExpressions_InSourceOrder()
    {
        var result = CssBindingRewriter.Rewrite(
            CssTestHelpers.ParseStylesheet(".a { color: v-bind(first); background: v-bind(second); }"),
            Salt);

        result.Bindings.Select(binding => binding.Expression).ShouldBe(new[] { "first", "second" });
        result.Bindings[0].Name.ShouldNotBe(result.Bindings[1].Name);
    }

    [Fact]
    public void Rewrite_IsDeterministic_AndComponentScoped()
    {
        var first = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind(color); }"), Salt);
        var second = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind(color); }"), Salt);
        var other = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind(color); }"), "zzzz9999");

        second.Bindings[0].Name.ShouldBe(first.Bindings[0].Name);
        second.Stylesheet.ShouldBe(first.Stylesheet);
        other.Bindings[0].Name.ShouldNotBe(first.Bindings[0].Name);
    }

    [Fact]
    public void Rewrite_IgnoresKeywordInsideStringLiteral()
    {
        var result = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { content: \"v-bind(x)\"; }"), Salt);

        // A v-bind inside a string literal is not a binding (upstream strips comments/strings before matching).
        result.Bindings.ShouldBeEmpty();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Rewrite_ReportsUnterminatedBinding()
    {
        var result = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind(color; }"), Salt);

        // The unbalanced '(' consumes to end of value; recoverable, no binding recorded.
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(CssErrorCode.UnterminatedCssBinding);
        result.Bindings.ShouldBeEmpty();
    }

    [Fact]
    public void Rewrite_ReportsEmptyBinding()
    {
        var result = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind(); }"), Salt);

        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(CssErrorCode.EmptyCssBinding);
        result.Bindings.ShouldBeEmpty();
    }

    [Fact]
    public void Rewrite_RewritesBindingsInKeyframesAndMedia()
    {
        var result = CssBindingRewriter.Rewrite(
            CssTestHelpers.ParseStylesheet(
                "@keyframes x { from { opacity: v-bind(a); } } @media screen { .b { color: v-bind(c); } }"),
            Salt);

        result.Bindings.Select(binding => binding.Expression).ShouldBe(new[] { "a", "c" });
    }

    [Fact]
    public void Rewrite_ComposesWithScoped()
    {
        // v-bind rewrite over the tree, then the scoped serializer: the value carries var(--hash) and the
        // selector carries the [data-v] attribute.
        var bound = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: v-bind(color); }"), Salt);

        var scoped = CssScopedRewriter.Rewrite(bound.Stylesheet, "data-v-test");

        scoped.ShouldBe(".a[data-v-test] {\n  color: var(--" + bound.Bindings[0].Name + ");\n}\n");
    }

    [Fact]
    public void Rewrite_LeavesBindingFreeStylesheetUnchanged()
    {
        var result = CssBindingRewriter.Rewrite(CssTestHelpers.ParseStylesheet(".a { color: red; }"), Salt);

        result.Bindings.ShouldBeEmpty();
        result.Diagnostics.ShouldBeEmpty();
        result.Stylesheet.ShouldBe(CssTestHelpers.ParseStylesheet(".a { color: red; }"));
    }
}
