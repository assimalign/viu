using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCssLayerEmitterTests
{
    [Fact]
    public void EmitDesignSystem_DefaultTheme_EmitsOrderedLayersPropertiesPreflightAndKeyframes()
    {
        var css = UtilityCssLayerEmitter.EmitDesignSystem(
            UtilityTheme.Default,
            CancellationToken.None);

        css.ShouldStartWith(UtilityCssLayerEmitter.LayerOrder);
        css.IndexOf("@layer theme {", StringComparison.Ordinal)
            .ShouldBeLessThan(
                css.IndexOf("@layer base {", StringComparison.Ordinal));
        css.ShouldContain("--color-taupe-950: oklch(14.7% 0.004 49.3);");
        css.ShouldContain("--text-9xl--line-height: 1;");
        css.ShouldContain("--default-font-family: var(--font-sans);");
        css.ShouldNotContain("\n    --blur: 8px;");
        css.ShouldContain("@keyframes bounce");
        css.ShouldContain("@keyframes ping");
        css.ShouldContain("@keyframes pulse");
        css.ShouldContain("@keyframes spin");
        css.ShouldContain("[hidden]:where(:not([hidden='until-found']))");
        css.ShouldNotContain("__DEFAULT_");
        css.ShouldNotContain("\r");
    }

    [Fact]
    public void EmitTheme_KeyframesFollowStableOrdinalOrder()
    {
        var css = UtilityCssLayerEmitter.EmitTheme(
            UtilityTheme.Default,
            CancellationToken.None);
        var positions = UtilityTheme.Default.Keyframes
            .Select(
                keyframe => css.IndexOf(
                    "@keyframes " + keyframe.Name,
                    StringComparison.Ordinal))
            .ToArray();

        positions.ShouldAllBe(position => position >= 0);
        positions.ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void EmitDesignSystem_PrefixedTheme_RewritesOwnedVariableReferences()
    {
        var parsed = UtilityThemeParser.Parse(
            """
            @theme prefix(viu) {
              --font-sans: Viu Sans, sans-serif;
            }
            """);

        parsed.Diagnostics.ShouldBeEmpty();
        var css = UtilityCssLayerEmitter.EmitDesignSystem(
            parsed.Theme,
            CancellationToken.None);

        css.ShouldContain("--viu-font-sans: Viu Sans, sans-serif;");
        css.ShouldContain(
            "--viu-default-font-family: var(--viu-font-sans);");
        css.ShouldContain(
            "font-family: var(--viu-default-font-family,");
        css.ShouldContain(
            "font-feature-settings: var(--viu-default-font-feature-settings, normal);");
    }

    [Fact]
    public void EmitDesignSystem_NormalMode_EmitsUsedThemeVariablesAndDependenciesOnly()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/theme#generating-all-css-variables
        var parsed = UtilityThemeParser.Parse(
            """
            @theme {
              --color-brand: var(--color-source);
              --color-source: #123456;
              --color-unused: #abcdef;
            }
            """);
        var resolution = UtilityCssRegistry.BuiltIn.Resolve(
            "bg-brand",
            parsed.Theme,
            CancellationToken.None);

        parsed.Diagnostics.ShouldBeEmpty();
        resolution.IsSuccess.ShouldBeTrue();
        var css = UtilityCssLayerEmitter.EmitDesignSystem(
            parsed.Theme,
            resolution.Metadata!.Css,
            UtilityThemeOptions.Default,
            CancellationToken.None);

        css.ShouldContain("--color-brand: var(--color-source);");
        css.ShouldContain("--color-source: #123456;");
        css.ShouldNotContain("--color-unused:");
    }

    [Fact]
    public void EmitDesignSystem_StaticImportMode_EmitsUnusedThemeVariables()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/theme#generating-all-css-variables
        var parsed = UtilityThemeParser.Parse(
            "@theme { --color-unused: #abcdef; }");

        var css = UtilityCssLayerEmitter.EmitDesignSystem(
            parsed.Theme,
            string.Empty,
            UtilityThemeOptions.Static,
            CancellationToken.None);

        css.ShouldContain("--color-unused: #abcdef;");
    }

    [Fact]
    public void EmitDesignSystem_InlineDeclaration_SubstitutesValueAndOmitsUnusedVariable()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/theme#referencing-other-variables
        var parsed = UtilityThemeParser.Parse(
            "@theme inline { --color-brand: var(--application-brand); }");
        var resolution = UtilityCssRegistry.BuiltIn.Resolve(
            "bg-brand",
            parsed.Theme,
            CancellationToken.None);

        resolution.IsSuccess.ShouldBeTrue();
        resolution.Metadata!.Css.ShouldContain(
            "background-color: var(--application-brand);");
        var css = UtilityCssLayerEmitter.EmitDesignSystem(
            parsed.Theme,
            resolution.Metadata.Css,
            UtilityThemeOptions.Default,
            CancellationToken.None);
        css.ShouldNotContain("--color-brand:");
    }

    [Fact]
    public void EmitTheme_ResetAnimationNamespace_DoesNotEmitOrphanedDefaultKeyframes()
    {
        var parsed = UtilityThemeParser.Parse(
            """
            @theme {
              --animate-*: initial;
            }
            """);

        parsed.Diagnostics.ShouldBeEmpty();
        var css = UtilityCssLayerEmitter.EmitTheme(
            parsed.Theme,
            CancellationToken.None);

        css.ShouldNotContain("@keyframes");
    }

    [Fact]
    public void EmitBase_CompleteV433Preflight_IncludesEveryCompatibilityArea()
    {
        // Compatibility behavior:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/preflight.css
        var css = UtilityCssLayerEmitter.EmitBase(
            UtilityTheme.Default,
            CancellationToken.None);

        var requiredFragments = new[]
        {
            "::backdrop",
            "-webkit-text-size-adjust: 100%",
            "abbr:where([title])",
            ":-moz-focusring:where(:not(iframe))",
            "list-style: none",
            "max-width: 100%",
            ":where(select:is([multiple], [size])) optgroup",
            "color-mix(in oklab, currentcolor 50%, transparent)",
            "::-webkit-date-and-time-value",
            "::-webkit-datetime-edit-millisecond-field",
            "::-webkit-calendar-picker-indicator",
            ":-moz-ui-invalid",
            "appearance: button",
            "[hidden]:where(:not([hidden='until-found']))",
        };
        foreach (var fragment in requiredFragments)
        {
            css.ShouldContain(fragment);
        }
    }

    [Fact]
    public void EmitDesignSystem_EquivalentThemes_ProducesIdenticalCss()
    {
        var first = UtilityThemeParser.Parse(
            "@theme { --color-brand: red; --zoom-card: 0.9; }");
        var second = UtilityThemeParser.Parse(
            "@theme { --zoom-card: 0.9; --color-brand: red; }");

        UtilityCssLayerEmitter.EmitDesignSystem(
                first.Theme,
                CancellationToken.None)
            .ShouldBe(
                UtilityCssLayerEmitter.EmitDesignSystem(
                    second.Theme,
                    CancellationToken.None));
    }

    [Fact]
    public void EmitDesignSystem_UsedComposedUtilities_EmitsTypedNonInheritedProperties()
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var usageCss =
            "--tw-blur: blur(8px); " +
            "--tw-gradient-stops: var(--tw-gradient-from); " +
            "--tw-mask-radial: radial-gradient(var(--tw-mask-radial-size)); " +
            "--tw-shadow: 0 1px var(--tw-shadow-color, #0001);";

        var css = UtilityCssLayerEmitter.EmitDesignSystem(
            UtilityTheme.Default,
            usageCss,
            UtilityThemeOptions.Default,
            CancellationToken.None);

        css.ShouldContain(
            """
            @property --tw-blur {
              syntax: "*";
              inherits: false;
            }
            """);
        css.ShouldContain(
            """
            @property --tw-gradient-from {
              syntax: "<color>";
              inherits: false;
              initial-value: #0000;
            }
            """);
        css.ShouldContain(
            """
            @property --tw-shadow-alpha {
              syntax: "<percentage>";
              inherits: false;
              initial-value: 100%;
            }
            """);
        css.ShouldContain(
            """
            @property --tw-mask-radial-size {
              syntax: "*";
              inherits: false;
              initial-value: farthest-corner;
            }
            """);
        css.IndexOf(
                "@property --tw-blur",
                StringComparison.Ordinal)
            .ShouldBeLessThan(
                css.IndexOf(
                    "@layer base {",
                    StringComparison.Ordinal));
        css.ShouldNotContain("@property --tw-backdrop-blur");
    }

    [Fact]
    public void EmitDesignSystem_Canceled_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityCssLayerEmitter.EmitDesignSystem(
                UtilityTheme.Default,
                cancellationSource.Token));
    }
}
