using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCssCompilerTests
{
    [Fact]
    public void Compile_CoreUtilityFamilies_EmitExpectedDeclarations()
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "hidden",
                "absolute",
                "flex-1",
                "flex-row",
                "items-center",
                "grid-cols-3",
                "col-span-2",
                "gap-4",
                "p-4",
                "-mb-4",
                "w-1/2",
                "h-screen",
                "size-8",
                "text-lg",
                "font-bold",
                "bg-blue-500",
                "border-2",
                "rounded-lg",
                "opacity-50",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(19);
        result.Css.ShouldContain("display: none;");
        result.Css.ShouldContain("position: absolute;");
        result.Css.ShouldContain("flex: 1;");
        result.Css.ShouldContain("flex-direction: row;");
        result.Css.ShouldContain("align-items: center;");
        result.Css.ShouldContain("grid-template-columns: repeat(3, minmax(0, 1fr));");
        result.Css.ShouldContain("grid-column: span 2 / span 2;");
        result.Css.ShouldContain("gap: calc(var(--spacing) * 4);");
        result.Css.ShouldContain("padding: calc(var(--spacing) * 4);");
        result.Css.ShouldContain(
            "margin-bottom: calc(calc(var(--spacing) * 4) * -1);");
        result.Css.ShouldContain("width: calc(1 / 2 * 100%);");
        result.Css.ShouldContain("height: 100vh;");
        result.Css.ShouldContain("width: calc(var(--spacing) * 8);");
        result.Css.ShouldContain("height: calc(var(--spacing) * 8);");
        result.Css.ShouldContain("font-size: var(--text-lg);");
        result.Css.ShouldContain("font-weight: var(--font-weight-bold);");
        result.Css.ShouldContain("background-color: var(--color-blue-500);");
        result.Css.ShouldContain("border-width: 2px;");
        result.Css.ShouldContain("border-radius: var(--radius-lg);");
        result.Css.ShouldContain("opacity: 0.5;");
    }

    [Fact]
    public void Compile_ArbitraryValuesPropertiesColorOpacityAndImportant_EmitSafeEscapedCss()
    {
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "w-[32px]",
                "grid-cols-[200px_1fr]",
                "[mask-type:luminance]",
                "bg-red-500/50",
                "disabled:opacity-50!",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Css.ShouldContain(@".w-\[32px\]");
        result.Css.ShouldContain("width: 32px;");
        result.Css.ShouldContain("grid-template-columns: 200px 1fr;");
        result.Css.ShouldContain("mask-type: luminance;");
        result.Css.ShouldContain(
            "background-color: color-mix(in oklab, var(--color-red-500) 50%, transparent);");
        result.Css.ShouldContain(@".disabled\:opacity-50\!:disabled");
        result.Css.ShouldContain("opacity: 0.5 !important;");
    }

    [Fact]
    public void Compile_StateDarkResponsiveAndCompoundVariants_EmitSelectorsAndMediaWrappers()
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/variants.test.ts
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "hover:bg-blue-500",
                "focus:bg-blue-500",
                "active:bg-blue-500",
                "dark:bg-gray-900",
                "sm:bg-blue-500",
                "2xl:flex",
                "group-hover:bg-blue-500",
                "peer-disabled:opacity-50",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Css.ShouldContain("@media (hover: hover)");
        result.Css.ShouldContain(@".hover\:bg-blue-500:hover");
        result.Css.ShouldContain(@".focus\:bg-blue-500:focus");
        result.Css.ShouldContain(@".active\:bg-blue-500:active");
        result.Css.ShouldContain("@media (prefers-color-scheme: dark)");
        result.Css.ShouldContain("@media (width >= 40rem)");
        result.Css.ShouldContain("@media (width >= 96rem)");
        result.Css.ShouldContain(@".\32 xl\:flex");
        result.Css.ShouldContain(@":where(.group):hover .group-hover\:bg-blue-500");
        result.Css.ShouldContain(@":where(.peer):disabled ~ .peer-disabled\:opacity-50");
    }

    [Fact]
    public void Compile_ReorderedAndRepeatedInputs_ProducesIdenticalOrderedLayer()
    {
        var first = UtilityCssCompiler.Compile(
            new[] { "rounded-lg", "p-4", "flex", "bg-blue-500", "p-4" });
        var second = UtilityCssCompiler.Compile(
            new[] { "bg-blue-500", "flex", "p-4", "rounded-lg" });

        first.Diagnostics.ShouldBeEmpty();
        second.Diagnostics.ShouldBeEmpty();
        first.Css.ShouldBe(second.Css);
        first.Rules.ShouldBe(second.Rules);
        first.Rules.Select(rule => rule.SortOrder)
            .ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void Compile_UnsupportedAndUnsafeCandidates_ReportsErrorsAndKeepsValidRules()
    {
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "flex",
                "prose",
                "-p-4",
                "[color$:red]",
                "[>img]:flex",
            });

        result.Rules.ShouldHaveSingleItem().CandidateText.ShouldBe("flex");
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldBe(
                new[]
                {
                    UtilityCssDiagnosticCode.UnsupportedUtility,
                    UtilityCssDiagnosticCode.UnsupportedNegativeForm,
                    UtilityCssDiagnosticCode.UnsafeArbitraryValue,
                    UtilityCssDiagnosticCode.UnsupportedVariant,
                });
    }
}
