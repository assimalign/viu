using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCssExpandedCatalogTests
{
    [Fact]
    public void Compile_LayoutLogicalSizingAndGridFamilies_EmitExpectedDeclarations()
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var candidates = new[]
        {
            "sr-only",
            "aspect-16/9",
            "columns-3",
            "contain-inline-size",
            "overflow-x-auto",
            "object-cover",
            "z-10",
            "basis-1/2",
            "grid-flow-col-dense",
            "auto-cols-fr",
            "justify-items-stretch",
            "col-start-2",
            "mbs-4",
            "pbe-2",
            "inline-1/2",
            "min-block-lh",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain("clip-path: inset(50%);");
        result.Css.ShouldContain("aspect-ratio: 16 / 9;");
        result.Css.ShouldContain("columns: 3;");
        result.Css.ShouldContain("--tw-contain-size: inline-size;");
        result.Css.ShouldContain("overflow-x: auto;");
        result.Css.ShouldContain("object-fit: cover;");
        result.Css.ShouldContain("z-index: 10;");
        result.Css.ShouldContain("flex-basis: calc(1 / 2 * 100%);");
        result.Css.ShouldContain("grid-auto-flow: column dense;");
        result.Css.ShouldContain("grid-auto-columns: minmax(0, 1fr);");
        result.Css.ShouldContain("justify-items: stretch;");
        result.Css.ShouldContain("grid-column-start: 2;");
        result.Css.ShouldContain("margin-block-start: calc(var(--spacing) * 4);");
        result.Css.ShouldContain("padding-block-end: calc(var(--spacing) * 2);");
        result.Css.ShouldContain("inline-size: calc(1 / 2 * 100%);");
        result.Css.ShouldContain("min-block-size: 1lh;");
    }

    [Fact]
    public void Compile_TypographyBackgroundBorderEffectAndMaskFamilies_EmitExpectedDeclarations()
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var candidates = new[]
        {
            "font-sans",
            "italic",
            "tabular-nums",
            "leading-6",
            "-tracking-tight",
            "line-clamp-3",
            "text-center",
            "decoration-dashed",
            "underline-offset-4",
            "bg-fixed",
            "bg-clip-text",
            "bg-linear-to-r",
            "border-s-2",
            "border-bs-blue-500",
            "rounded-ss-lg",
            "outline-dashed",
            "shadow-lg",
            "inset-shadow-sm",
            "text-shadow-sm",
            "mask-add",
            "mask-type-luminance",
            "mask-cover",
            "mask-radial-at-center",
            "blur-sm",
            "-hue-rotate-30",
            "backdrop-opacity-50",
            "filter-none",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain("font-family: var(--font-sans);");
        result.Css.ShouldContain("font-style: italic;");
        result.Css.ShouldContain("--tw-numeric-spacing: tabular-nums;");
        result.Css.ShouldContain("line-height: calc(var(--spacing) * 6);");
        result.Css.ShouldContain(
            "letter-spacing: calc(var(--tracking-tight) * -1);");
        result.Css.ShouldContain("-webkit-line-clamp: 3;");
        result.Css.ShouldContain("text-align: center;");
        result.Css.ShouldContain("text-decoration-style: dashed;");
        result.Css.ShouldContain("text-underline-offset: 4px;");
        result.Css.ShouldContain("background-attachment: fixed;");
        result.Css.ShouldContain("background-clip: text;");
        result.Css.ShouldContain(
            "--tw-gradient-position: to right in oklab;");
        result.Css.ShouldContain(
            "background-image: linear-gradient(var(--tw-gradient-stops));");
        result.Css.ShouldContain("border-inline-start-width: 2px;");
        result.Css.ShouldContain(
            "border-block-start-color: var(--color-blue-500);");
        result.Css.ShouldContain(
            "border-start-start-radius: var(--radius-lg);");
        result.Css.ShouldContain("outline-style: dashed;");
        result.Css.ShouldContain("--tw-shadow:");
        result.Css.ShouldContain("--tw-inset-shadow:");
        result.Css.ShouldContain(
            "box-shadow: var(--tw-inset-shadow), var(--tw-inset-ring-shadow), var(--tw-ring-offset-shadow), var(--tw-ring-shadow), var(--tw-shadow);");
        result.Css.ShouldContain(
            "text-shadow:");
        result.Css.ShouldContain(
            "var(--tw-text-shadow-color,");
        result.Css.ShouldContain("mask-composite: add;");
        result.Css.ShouldContain("mask-type: luminance;");
        result.Css.ShouldContain("mask-size: cover;");
        result.Css.ShouldContain(
            "--tw-mask-radial-position: center;");
        result.Css.ShouldContain("--tw-blur: blur(var(--blur-sm));");
        result.Css.ShouldContain("--tw-hue-rotate: hue-rotate(calc(30deg * -1));");
        result.Css.ShouldContain(
            "--tw-backdrop-opacity: opacity(50%);");
        result.Css.ShouldContain("filter: none;");
    }

    [Fact]
    public void Compile_TableTransitionTransformInteractionAndSvgFamilies_EmitExpectedDeclarations()
    {
        var candidates = new[]
        {
            "border-collapse",
            "border-spacing-x-2",
            "table-fixed",
            "caption-bottom",
            "transition-colors",
            "transition-discrete",
            "duration-150",
            "ease-in-out",
            "animate-spin",
            "translate-1/2",
            "rotate-x-45",
            "scale-105",
            "transform-3d",
            "zoom-125",
            "cursor-pointer",
            "scrollbar-thin",
            "scrollbar-gutter-both",
            "scrollbar-thumb-blue-500",
            "tab-4",
            "field-sizing-content",
            "fill-blue-500",
            "stroke-2",
            "forced-color-adjust-none",
            "placeholder-gray-500",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain("border-collapse: collapse;");
        result.Css.ShouldContain(
            "--tw-border-spacing-x: calc(var(--spacing) * 2);");
        result.Css.ShouldContain("table-layout: fixed;");
        result.Css.ShouldContain("caption-side: bottom;");
        result.Css.ShouldContain(
            "transition-property: color, background-color, border-color, outline-color, text-decoration-color, fill, stroke, --tw-gradient-from, --tw-gradient-via, --tw-gradient-to;");
        result.Css.ShouldContain("transition-behavior: allow-discrete;");
        result.Css.ShouldContain("transition-duration: 150ms;");
        result.Css.ShouldContain(
            "transition-timing-function: var(--ease-in-out);");
        result.Css.ShouldContain("animation: var(--animate-spin);");
        result.Css.ShouldContain(
            "--tw-translate-x: calc(1 / 2 * 100%);");
        result.Css.ShouldContain(
            "translate: var(--tw-translate-x) var(--tw-translate-y);");
        result.Css.ShouldContain("--tw-rotate-x: rotateX(45deg);");
        result.Css.ShouldContain(
            "transform: var(--tw-rotate-x,) var(--tw-rotate-y,) var(--tw-rotate-z,)");
        result.Css.ShouldContain("--tw-scale-x: 1.05;");
        result.Css.ShouldContain(
            "scale: var(--tw-scale-x) var(--tw-scale-y);");
        result.Css.ShouldContain("transform-style: preserve-3d;");
        result.Css.ShouldContain("zoom: 125%;");
        result.Css.ShouldContain("cursor: pointer;");
        result.Css.ShouldContain("scrollbar-width: thin;");
        result.Css.ShouldContain("scrollbar-gutter: stable both-edges;");
        result.Css.ShouldContain("--tw-scrollbar-thumb: var(--color-blue-500);");
        result.Css.ShouldContain("tab-size: 4;");
        result.Css.ShouldContain("field-sizing: content;");
        result.Css.ShouldContain("fill: var(--color-blue-500);");
        result.Css.ShouldContain("stroke-width: 2;");
        result.Css.ShouldContain("forced-color-adjust: none;");
        result.Css.ShouldContain("::placeholder");
    }

    [Fact]
    public void GetCompletions_ExplicitTheme_ExpandsEverySupportedThemeBackedFamily()
    {
        var parseResult = UtilityThemeParser.Parse(
            """
            @theme {
              --color-brand: #123456;
              --spacing-panel: 17rem;
              --font-display: "Example Sans";
              --tracking-news: 0.2em;
              --leading-copy: 1.45;
              --tab-size-code: 3;
              --container-card: 34rem;
              --shadow-card: 0 1px 4px #0003;
              --blur-mist: 7px;
              --perspective-stage: 700px;
              --zoom-reading: 115%;
              --aspect-photo: 4 / 3;
              --ease-spring: linear(0, 1);
              --animate-wiggle: wiggle 1s linear infinite;
              --breakpoint-phone: 30rem;
            }
            """);
        parseResult.Diagnostics.ShouldBeEmpty();

        var registry = UtilityCssRegistry.BuiltIn;
        var completions = registry.GetCompletions(
            string.Empty,
            parseResult.Theme);
        var expectedCandidates = new[]
        {
            "bg-brand",
            "p-panel",
            "font-display",
            "tracking-news",
            "leading-copy",
            "tab-code",
            "columns-card",
            "shadow-card",
            "blur-mist",
            "backdrop-blur-mist",
            "perspective-stage",
            "zoom-reading",
            "aspect-photo",
            "ease-spring",
            "animate-wiggle",
            "phone:bg-brand",
        };

        foreach (var expectedCandidate in expectedCandidates)
        {
            var completion = completions.Single(
                item => item.CandidateText == expectedCandidate);
            var resolution = registry.Resolve(
                expectedCandidate,
                parseResult.Theme,
                CancellationToken.None);

            resolution.IsSuccess.ShouldBeTrue(expectedCandidate);
            resolution.Metadata.ShouldBe(completion);
        }
    }

    [Fact]
    public void Compile_ExpandedVariantFamilies_EmitSelectorsAndConditionalWrappers()
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/variants.test.ts
        var candidates = new[]
        {
            "first:flex",
            "nth-[2n+1]:flex",
            "before:block",
            "aria-disabled:flex",
            "data-[state=open]:flex",
            "supports-[display:grid]:grid",
            "min-md:max-xl:flex",
            "@md/sidebar:flex",
            "group-data-[state=open]/card:flex",
            "peer-aria-disabled:flex",
            "has-[>img]:block",
            "in-focus:flex",
            "not-disabled:flex",
            "rtl:flex",
            "print:flex",
            "motion-safe:flex",
            "contrast-more:flex",
            "landscape:flex",
            "forced-colors:flex",
            "starting:flex",
            "[@media(width>=30rem)]:flex",
            "[&:nth-child(3)]:underline",
            "dark:motion-safe:hover:focus:flex",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain(":first-child");
        result.Css.ShouldContain(":nth-child(2n+1)");
        result.Css.ShouldContain("::before");
        result.Css.ShouldContain("content: var(--tw-content);");
        result.Css.ShouldContain("[aria-disabled=\"true\"]");
        result.Css.ShouldContain("[data-state=\"open\"]");
        result.Css.ShouldContain("@supports (display:grid)");
        result.Css.ShouldContain("@media (width >= 48rem)");
        result.Css.ShouldContain("@media (width < 80rem)");
        result.Css.ShouldContain("@container sidebar (width >= 28rem)");
        result.Css.ShouldContain(":where(.group\\/card)[data-state=\"open\"]");
        result.Css.ShouldContain(":where(.peer)[aria-disabled=\"true\"] ~");
        result.Css.ShouldContain(":has(>img)");
        result.Css.ShouldContain(":where(*:focus)");
        result.Css.ShouldContain(":not(*:disabled)");
        result.Css.ShouldContain(":where(:dir(rtl), [dir=\"rtl\"], [dir=\"rtl\"] *)");
        result.Css.ShouldContain("@media print");
        result.Css.ShouldContain(
            "@media (prefers-reduced-motion: no-preference)");
        result.Css.ShouldContain("@media (prefers-contrast: more)");
        result.Css.ShouldContain("@media (orientation: landscape)");
        result.Css.ShouldContain("@media (forced-colors: active)");
        result.Css.ShouldContain("@starting-style");
        result.Css.ShouldContain("@media (width>=30rem)");
        result.Css.ShouldContain(":nth-child(3)");
        result.Css.ShouldContain("@media (prefers-color-scheme: dark)");
        result.Css.ShouldContain("@media (hover: hover)");
        result.Css.ShouldContain(":hover:focus");
    }
}
