using System;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityAdditionalResolutionTests
{
    [Fact]
    public void Compile_LayoutCompatibilityCandidates_EmitTaggedVersionDeclarations()
    {
        // Upstream contract:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var candidates = new[]
        {
            "aspect-8.5/11",
            "auto-cols-12",
            "auto-rows-12",
            "break-words",
            "col-span-17",
            "col-span-[var(--column-count)]",
            "row-span-17",
            "row-span-[var(--row-count)]",
            "contain-[unset]",
            "divide-dashed",
            "flex-99",
            "flex-1/2",
            "grid-cols-99",
            "grid-rows-99",
            "object-top-left",
            "object-top-right",
            "object-bottom-left",
            "object-bottom-right",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain("aspect-ratio: 8.5 / 11;");
        result.Css.ShouldContain("grid-auto-columns: calc(var(--spacing) * 12);");
        result.Css.ShouldContain("grid-auto-rows: calc(var(--spacing) * 12);");
        result.Css.ShouldContain("overflow-wrap: break-word;");
        result.Css.ShouldContain("grid-column: span 17 / span 17;");
        result.Css.ShouldContain(
            "grid-column: span var(--column-count) / span var(--column-count);");
        result.Css.ShouldContain("grid-row: span 17 / span 17;");
        result.Css.ShouldContain(
            "grid-row: span var(--row-count) / span var(--row-count);");
        result.Css.ShouldContain("contain: unset;");
        result.Css.ShouldContain("--tw-border-style: dashed;");
        result.Css.ShouldContain("border-style: dashed;");
        result.Css.ShouldContain("flex: 99;");
        result.Css.ShouldContain("flex: calc(1 / 2 * 100%);");
        result.Css.ShouldContain(
            "grid-template-columns: repeat(99, minmax(0, 1fr));");
        result.Css.ShouldContain(
            "grid-template-rows: repeat(99, minmax(0, 1fr));");
        result.Css.ShouldContain("object-position: left top;");
        result.Css.ShouldContain("object-position: right top;");
        result.Css.ShouldContain("object-position: left bottom;");
        result.Css.ShouldContain("object-position: right bottom;");
    }

    [Fact]
    public void Compile_TypographyCompatibilityCandidates_InferSizeColorAndModifiers()
    {
        // Upstream contract:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var candidates = new[]
        {
            "decoration-123",
            "font-stretch-ultra-expanded",
            "font-stretch-50%",
            "font-stretch-200%",
            "text-[12px]/6",
            "text-[50%]/6",
            "text-[xx-large]/6",
            "text-[larger]/6",
            "text-[length:var(--my-size)]",
            "text-[percentage:var(--my-size)]",
            "text-[absolute-size:var(--my-size)]",
            "text-[relative-size:var(--my-size)]",
            "text-[var(--my-color)]/50",
            "text-[color:var(--my-color)]/[50%]",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain("text-decoration-thickness: 123px;");
        result.Css.ShouldContain("font-stretch: ultra-expanded;");
        result.Css.ShouldContain("font-stretch: 50%;");
        result.Css.ShouldContain("font-stretch: 200%;");
        result.Css.ShouldContain("font-size: 12px;");
        result.Css.ShouldContain("font-size: 50%;");
        result.Css.ShouldContain("font-size: xx-large;");
        result.Css.ShouldContain("font-size: larger;");
        result.Css.ShouldContain("font-size: var(--my-size);");
        result.Css.ShouldContain("line-height: calc(var(--spacing) * 6);");
        result.Css.ShouldContain(
            "color: color-mix(in oklab, var(--my-color) 50%, transparent);");
    }

    [Theory]
    [InlineData("aspect-1.23/4.56")]
    [InlineData("auto-cols-0.1")]
    [InlineData("auto-rows-0.1")]
    [InlineData("col-span-17/foo")]
    [InlineData("row-span-[var(--row-count)]/foo")]
    [InlineData("contain-[unset]/foo")]
    [InlineData("divide-dashed/foo")]
    [InlineData("flex--1")]
    [InlineData("flex-1/2/foo")]
    [InlineData("grid-cols-0")]
    [InlineData("grid-rows-0")]
    [InlineData("font-stretch-20%")]
    [InlineData("font-stretch-50.5%")]
    [InlineData("font-stretch-400%")]
    [InlineData("object-top-left/foo")]
    [InlineData("text-[12px]/foo")]
    public void Resolve_InvalidCompatibilityCandidate_ReturnsDiagnostic(
        string candidate)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeFalse();
        result.Metadata.ShouldBeNull();
        result.Diagnostics.ShouldNotBeEmpty();
        result.Diagnostics.All(
            diagnostic => diagnostic.Severity == UtilityCssDiagnosticSeverity.Error)
            .ShouldBeTrue();
    }
}
