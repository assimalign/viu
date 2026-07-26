using System.Collections.Generic;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCssV433AcceptanceGateTests
{
    [Fact]
    public void Resolve_UtilityFamiliesWithoutModifierSupport_RejectSlashModifier()
    {
        // Compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts
        var candidateBases = new List<string>();
        AddValues(
            candidateBases,
            "content",
            "around",
            "baseline",
            "between",
            "center",
            "end",
            "evenly",
            "normal",
            "start",
            "stretch");
        candidateBases.AddRange(
            new[]
            {
                "flex-col-reverse",
                "flex-col",
                "flex-row-reverse",
                "flex-row",
                "grow",
                "grow-0",
                "grow-[123]",
            });
        AddValues(
            candidateBases,
            "items",
            "baseline",
            "center",
            "end",
            "start",
            "stretch");
        AddValues(
            candidateBases,
            "justify",
            "around",
            "between",
            "center",
            "end",
            "evenly",
            "normal",
            "start",
            "stretch");
        AddValues(
            candidateBases,
            "justify-items",
            "center",
            "end",
            "start",
            "stretch");
        AddValues(
            candidateBases,
            "justify-self",
            "auto",
            "baseline",
            "center",
            "end",
            "start",
            "stretch");
        AddPositionValues(candidateBases, "origin");
        candidateBases.AddRange(
            new[]
            {
                "origin-[12px_24px]",
                "perspective-[500px]",
                "perspective-dramatic",
                "perspective-none",
                "perspective-normal",
            });
        AddPositionValues(candidateBases, "perspective-origin");
        AddValues(
            candidateBases,
            "place-content",
            "around",
            "baseline",
            "between",
            "center",
            "end",
            "evenly",
            "start",
            "stretch");
        AddValues(
            candidateBases,
            "place-items",
            "baseline",
            "center",
            "end",
            "start",
            "stretch");
        AddValues(
            candidateBases,
            "place-self",
            "auto",
            "center",
            "end",
            "start",
            "stretch");
        AddValues(
            candidateBases,
            "self",
            "auto",
            "baseline",
            "center",
            "end",
            "start",
            "stretch");
        candidateBases.AddRange(
            new[]
            {
                "shrink",
                "shrink-0",
                "shrink-[123]",
                "transform",
                "transform-[matrix(1,0,0,1,0,0)]",
                "transform-cpu",
                "transform-gpu",
                "transform-none",
            });

        foreach (var candidateBase in candidateBases)
        {
            var candidate = candidateBase + "/foo";
            var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

            result.IsSuccess.ShouldBeFalse(candidate);
            result.Metadata.ShouldBeNull();
            result.Diagnostics.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public void Compile_FlexWrappingAndFilterForms_EmitTaggedVersionDeclarations()
    {
        var candidates = new[]
        {
            "flex-wrap",
            "flex-nowrap",
            "flex-wrap-reverse",
            "filter",
            "filter-[blur(2px)]",
            "backdrop-filter",
            "backdrop-filter-[blur(3px)]",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain("flex-wrap: wrap;");
        result.Css.ShouldContain("flex-wrap: nowrap;");
        result.Css.ShouldContain("flex-wrap: wrap-reverse;");
        result.Css.ShouldContain(
            "filter: var(--tw-blur,) var(--tw-brightness,)");
        result.Css.ShouldContain("filter: blur(2px);");
        result.Css.ShouldContain(
            "-webkit-backdrop-filter: var(--tw-backdrop-blur,)");
        result.Css.ShouldContain(
            "backdrop-filter: var(--tw-backdrop-blur,)");
        result.Css.ShouldContain("-webkit-backdrop-filter: blur(3px);");
        result.Css.ShouldContain("backdrop-filter: blur(3px);");
    }

    [Theory]
    [InlineData("-bg-linear-[to_bottom]")]
    [InlineData("-bg-linear-to-br")]
    [InlineData("-underline-offset-auto")]
    [InlineData("-z-auto")]
    [InlineData("px-0.375")]
    [InlineData("px-2.50")]
    [InlineData("scale-0.375")]
    [InlineData("scale-2.50")]
    [InlineData("max-w-auto")]
    [InlineData("max-h-auto")]
    [InlineData("max-inline-auto")]
    [InlineData("max-block-auto")]
    [InlineData("inset-shadow")]
    [InlineData("justify-self-baseline")]
    public void Resolve_UnsupportedTaggedVersionCandidate_ReturnsDiagnostic(
        string candidate)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeFalse();
        result.Metadata.ShouldBeNull();
        result.Diagnostics.ShouldNotBeEmpty();
    }

    [Fact]
    public void Resolve_DefaultDropShadowWithUnknownModifier_IgnoresModifierLikeTaggedVersion()
    {
        // Tailwind CSS v4.3.3 intentionally ignores an unresolved modifier only for the default
        // drop-shadow branch:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts#L4289-L4322
        var result = UtilityCssRegistry.BuiltIn.Resolve("drop-shadow/foo");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Css.ShouldContain(
            "--tw-drop-shadow-size: drop-shadow(");
        result.Metadata.Css.ShouldNotContain(
            "--tw-drop-shadow-alpha:");
    }

    [Fact]
    public void Compile_NearbyValidForms_RemainSupported()
    {
        var candidates = new[]
        {
            "px-2.5",
            "scale-105",
            "scale-[1.055]",
            "-bg-linear-45",
            "-bg-linear-[45deg]",
            "underline-offset-auto",
            "z-auto",
            "inset-shadow-sm",
            "justify-self-auto",
        };

        var result = UtilityCssCompiler.Compile(candidates);

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.Count.ShouldBe(candidates.Length);
        result.Css.ShouldContain(
            "padding-inline: calc(var(--spacing) * 2.5);");
        result.Css.ShouldContain("scale: 1.055;");
        result.Css.ShouldContain("--tw-gradient-position: calc(45deg * -1)");
        result.Css.ShouldContain("text-underline-offset: auto;");
        result.Css.ShouldContain("z-index: auto;");
        result.Css.ShouldContain("justify-self: auto;");
    }

    [Fact]
    public void Resolve_BareInsetShadowWithDefaultThemeToken_RemainsSupported()
    {
        var themeResult = UtilityThemeParser.Parse(
            """
            @theme {
              --inset-shadow: inset 0 1px red;
            }
            """);
        themeResult.Diagnostics.ShouldBeEmpty();

        var result = UtilityCssRegistry.BuiltIn.Resolve(
            "inset-shadow",
            themeResult.Theme,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Css.ShouldContain(
            "--tw-inset-shadow: inset 0 1px var(--tw-inset-shadow-color, red);");
    }

    [Theory]
    [InlineData(
        "aria-[valuenow_=_\"1\"]:flex",
        "[aria-valuenow = \"1\"]")]
    [InlineData(
        "data-[potato_^=_\"salad\"]:flex",
        "[data-potato ^= \"salad\"]")]
    [InlineData(
        "data-[potato_=_\"salad\"]:flex",
        "[data-potato = \"salad\"]")]
    public void Compile_AttributeVariantWithOperatorWhitespace_PreservesSelector(
        string candidate,
        string expectedSelector)
    {
        // Compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/variants.ts
        var result = UtilityCssCompiler.Compile(
            new[] { candidate });

        result.Diagnostics.ShouldBeEmpty();
        result.Rules.ShouldHaveSingleItem();
        result.Css.ShouldContain(expectedSelector);
    }

    [Fact]
    public void Parse_AttributeVariantWithSplitOperator_RemainsInvalid()
    {
        var result = UtilityCandidateParser.Parse(
            "data-[foo_^_=_\"bar\"]:flex");

        result.IsSuccess.ShouldBeFalse();
        result.Candidate.ShouldBeNull();
        result.Diagnostics.ShouldNotBeEmpty();
    }

    private static void AddPositionValues(
        ICollection<string> candidates,
        string root) =>
        AddValues(
            candidates,
            root,
            "center",
            "top",
            "top-right",
            "right",
            "bottom-right",
            "bottom",
            "bottom-left",
            "left",
            "top-left");

    private static void AddValues(
        ICollection<string> candidates,
        string root,
        params string[] values)
    {
        foreach (var value in values)
        {
            candidates.Add(root + "-" + value);
        }
    }
}
