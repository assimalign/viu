using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityDefaultThemeTests
{
    [Fact]
    public void Default_CompleteV433Inventory_ExposesEveryNamespaceAndKeyframe()
    {
        // Compatibility inventory:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/theme.css
        var theme = UtilityTheme.Default;

        theme.Properties.Count.ShouldBe(419);
        theme.NamespaceNames.Count.ShouldBe(21);
        theme.Colors.Count.ShouldBe(288);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.FontFamily)
            .Count.ShouldBe(3);
        theme.FontSizes.Count.ShouldBe(13);
        theme.FontWeights.Count.ShouldBe(9);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.LetterSpacing)
            .Count.ShouldBe(6);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.LineHeight)
            .Count.ShouldBe(5);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.Container)
            .Count.ShouldBe(13);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.InsetShadow)
            .Count.ShouldBe(3);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.DropShadow)
            .Count.ShouldBe(7);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.TextShadow)
            .Count.ShouldBe(5);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.Perspective)
            .Count.ShouldBe(5);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.Animation)
            .Count.ShouldBe(4);
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.TabSize)
            .ShouldBeEmpty();
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.Zoom)
            .ShouldBeEmpty();

        theme.Keyframes.Select(keyframe => keyframe.Name)
            .ShouldBe(new[] { "bounce", "ping", "pulse", "spin" });
    }

    [Fact]
    public void Default_CompletePropertyVector_MatchesPinnedV433Fingerprint()
    {
        var canonical = string.Join(
            "\n",
            UtilityTheme.Default.Properties.Select(
                property =>
                    property.Name +
                    "|" +
                    property.Value +
                    "|" +
                    (int)property.Options));
        var fingerprint = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();

        fingerprint.ShouldBe(
            "8f6f2c62a520817eb04a648908dfe7635d3eb286474c8c6602fb70a29105fd22");
    }

    [Fact]
    public void Default_ColorsIncludeEveryV433PaletteAndUseVariableResolution()
    {
        var paletteNames = new[]
        {
            "red",
            "orange",
            "amber",
            "yellow",
            "lime",
            "green",
            "emerald",
            "teal",
            "cyan",
            "sky",
            "blue",
            "indigo",
            "violet",
            "purple",
            "fuchsia",
            "pink",
            "rose",
            "slate",
            "gray",
            "zinc",
            "neutral",
            "stone",
            "mauve",
            "olive",
            "mist",
            "taupe",
        };
        var shades = new[]
        {
            "50",
            "100",
            "200",
            "300",
            "400",
            "500",
            "600",
            "700",
            "800",
            "900",
            "950",
        };

        foreach (var paletteName in paletteNames)
        {
            foreach (var shade in shades)
            {
                UtilityTheme.Default.TryGetColor(
                        paletteName + "-" + shade,
                        out var value)
                    .ShouldBeTrue(paletteName + "-" + shade);
                value.ShouldBe(
                    "var(--color-" +
                    paletteName +
                    "-" +
                    shade +
                    ")");
            }
        }

        UtilityTheme.Default.TryGetColor("black", out var black)
            .ShouldBeTrue();
        black.ShouldBe("var(--color-black)");
        UtilityTheme.Default.TryGetColor("white", out var white)
            .ShouldBeTrue();
        white.ShouldBe("var(--color-white)");
        UtilityTheme.Default.TryGetNamespaceRawValue(
                UtilityThemeNamespaceNames.Color,
                "blue-500",
                out var blue)
            .ShouldBeTrue();
        blue.ShouldBe("oklch(62.3% 0.214 259.815)");
    }

    [Fact]
    public void Default_SpacingScaleResolvesCanonicalQuarterStepMultiplier()
    {
        var theme = UtilityTheme.Default;

        theme.TryGetSpacing("4", out var four).ShouldBeTrue();
        four.ShouldBe("calc(var(--spacing) * 4)");
        theme.TryGetSpacing("18", out var eighteen).ShouldBeTrue();
        eighteen.ShouldBe("calc(var(--spacing) * 18)");
        theme.TryGetSpacing("2.5", out var twoAndOneHalf).ShouldBeTrue();
        twoAndOneHalf.ShouldBe("calc(var(--spacing) * 2.5)");
        theme.TryGetSpacing("0.125", out _).ShouldBeFalse();
        theme.TryGetSpacing("0.375", out _).ShouldBeFalse();
        theme.TryGetSpacing("2.50", out _).ShouldBeFalse();
        theme.TryGetSpacing("-1", out _).ShouldBeFalse();
        theme.TryGetSpacing("word", out _).ShouldBeFalse();
        theme.TryGetNamespaceRawValue(
                UtilityThemeNamespaceNames.Spacing,
                "4",
                out var rawFour)
            .ShouldBeTrue();
        rawFour.ShouldBe("calc(0.25rem * 4)");
    }

    [Fact]
    public void Parse_AllDocumentedNamespaces_ProjectThroughGenericThemeApi()
    {
        // Namespace reference:
        // https://tailwindcss.com/docs/theme#theme-variable-namespaces
        var result = UtilityThemeParser.Parse(
            """
            @theme {
              --font-display: Display, sans-serif;
              --tracking-display: -0.075em;
              --leading-display: 0.95;
              --tab-size-github: 8;
              --container-card: 30rem;
              --shadow-card: 0 3px 12px #0003;
              --inset-shadow-card: inset 0 1px #fff3;
              --drop-shadow-card: 0 2px 2px #0004;
              --text-shadow-card: 0 1px 1px #0004;
              --blur-card: 10px;
              --perspective-card: 720px;
              --zoom-compact: 0.8;
              --aspect-photo: 4 / 3;
              --ease-spring: linear(0, 1);
              --animate-reveal: reveal 300ms ease-out;
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.FontFamily,
            "display",
            "var(--font-display)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.LetterSpacing,
            "display",
            "var(--tracking-display)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.LineHeight,
            "display",
            "var(--leading-display)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.TabSize,
            "github",
            "var(--tab-size-github)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.Container,
            "card",
            "var(--container-card)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.Shadow,
            "card",
            "var(--shadow-card)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.InsetShadow,
            "card",
            "var(--inset-shadow-card)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.DropShadow,
            "card",
            "var(--drop-shadow-card)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.TextShadow,
            "card",
            "var(--text-shadow-card)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.Blur,
            "card",
            "var(--blur-card)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.Perspective,
            "card",
            "var(--perspective-card)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.Zoom,
            "compact",
            "var(--zoom-compact)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.AspectRatio,
            "photo",
            "var(--aspect-photo)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.TransitionTiming,
            "spring",
            "var(--ease-spring)");
        AssertNamespaceValue(
            result.Theme,
            UtilityThemeNamespaceNames.Animation,
            "reveal",
            "var(--animate-reveal)");
    }

    [Fact]
    public void Default_CompoundMetadataRemainPropertiesButNotStandaloneTokens()
    {
        var theme = UtilityTheme.Default;

        theme.TryGetProperty(
                "--text-sm--line-height",
                out var lineHeight)
            .ShouldBeTrue();
        lineHeight.ShouldNotBeNull();
        lineHeight.Value.ShouldBe("calc(1.25 / 0.875)");
        theme.GetNamespaceTokens(UtilityThemeNamespaceNames.FontSize)
            .Any(token => token.Name.Contains("--", StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    private static void AssertNamespaceValue(
        UtilityTheme theme,
        string namespaceName,
        string tokenName,
        string expected)
    {
        theme.TryGetNamespaceValue(
                namespaceName,
                tokenName,
                out var value)
            .ShouldBeTrue(namespaceName + ":" + tokenName);
        value.ShouldBe(expected);
    }
}
