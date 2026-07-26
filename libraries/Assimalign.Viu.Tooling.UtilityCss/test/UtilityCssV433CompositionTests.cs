using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCssV433CompositionTests
{
    [Fact]
    public void Resolve_OutlineFamily_EmitsComposableStyleAndForcedColorFallback()
    {
        // Tailwind CSS v4.3.3 compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts#L4741-L4838
        var outline = UtilityCssRegistry.BuiltIn.Resolve("outline");
        var none = UtilityCssRegistry.BuiltIn.Resolve("outline-none");
        var hidden = UtilityCssRegistry.BuiltIn.Resolve("outline-hidden");
        var dashed = UtilityCssRegistry.BuiltIn.Resolve("outline-dashed");
        var width = UtilityCssRegistry.BuiltIn.Resolve("outline-[1.5]");

        outline.IsSuccess.ShouldBeTrue();
        outline.Metadata!.Css.ShouldContain(
            "outline-style: var(--tw-outline-style);");
        outline.Metadata.Css.ShouldContain("outline-width: 1px;");
        none.Metadata!.Css.ShouldContain("--tw-outline-style: none;");
        none.Metadata.Css.ShouldNotContain("@media (forced-colors: active)");
        hidden.Metadata!.Css.ShouldContain("--tw-outline-style: none;");
        hidden.Metadata.Css.ShouldContain("@media (forced-colors: active)");
        hidden.Metadata.Css.ShouldContain("outline: 2px solid transparent;");
        hidden.Metadata.Css.ShouldContain("outline-offset: 2px;");
        dashed.Metadata!.Css.ShouldContain("--tw-outline-style: dashed;");
        dashed.Metadata.Css.ShouldContain("outline-style: dashed;");
        width.Metadata!.Css.ShouldContain(
            "outline-style: var(--tw-outline-style);");
        width.Metadata.Css.ShouldContain("outline-width: 1.5px;");

        var css = EmitDesignSystem(
            new[] { "outline", "outline-hidden" },
            UtilityTheme.Default);
        css.ShouldContain(
            """
            @property --tw-outline-style {
              syntax: "*";
              inherits: false;
              initial-value: solid;
            }
            """);
    }

    [Fact]
    public void Resolve_NamedTextAndLeading_ComposeThroughLeadingVariable()
    {
        // Tailwind CSS v4.3.3 compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts#L4645-L4662
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts#L4958-L5012
        var text = UtilityCssRegistry.BuiltIn.Resolve("text-sm");
        var leading = UtilityCssRegistry.BuiltIn.Resolve("leading-6");

        text.IsSuccess.ShouldBeTrue();
        text.Metadata!.Css.ShouldContain("font-size: var(--text-sm);");
        text.Metadata.Css.ShouldContain(
            "line-height: var(--tw-leading, var(--text-sm--line-height));");
        leading.IsSuccess.ShouldBeTrue();
        leading.Metadata!.Css.ShouldContain(
            "--tw-leading: calc(var(--spacing) * 6);");
        leading.Metadata.Css.ShouldContain(
            "line-height: calc(var(--spacing) * 6);");

        var parsedTheme = UtilityThemeParser.Parse(
            """
            @theme {
              --text-title: 2rem;
              --text-title--line-height: 1.2;
              --leading-none: 2;
            }
            """);
        parsedTheme.Diagnostics.ShouldBeEmpty();
        var customText = UtilityCssRegistry.BuiltIn.Resolve(
            "text-title",
            parsedTheme.Theme,
            CancellationToken.None);
        var customLeading = UtilityCssRegistry.BuiltIn.Resolve(
            "leading-none",
            parsedTheme.Theme,
            CancellationToken.None);

        customText.Metadata!.Css.ShouldContain(
            "line-height: var(--tw-leading, var(--text-title--line-height));");
        customLeading.Metadata!.Css.ShouldContain(
            "--tw-leading: var(--leading-none);");

        var css = EmitDesignSystem(
            new[] { "text-sm", "leading-6" },
            UtilityTheme.Default);
        css.ShouldContain(
            """
            @property --tw-leading {
              syntax: "*";
              inherits: false;
            }
            """);
    }

    [Fact]
    public void Resolve_TransitionFamily_EmitsV433PropertyAndDefaultComposition()
    {
        // Tailwind CSS v4.3.3 compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts#L4443-L4570
        var transition = UtilityCssRegistry.BuiltIn.Resolve("transition");
        var colors = UtilityCssRegistry.BuiltIn.Resolve("transition-colors");
        var duration = UtilityCssRegistry.BuiltIn.Resolve("duration-150");
        var easing = UtilityCssRegistry.BuiltIn.Resolve("ease-in-out");
        var initialEasing = UtilityCssRegistry.BuiltIn.Resolve("ease-initial");

        transition.Metadata!.Css.ShouldContain(
            "transition-property: color, background-color, border-color, outline-color, text-decoration-color, fill, stroke, --tw-gradient-from, --tw-gradient-via, --tw-gradient-to, opacity, box-shadow, transform, translate, scale, rotate, filter, -webkit-backdrop-filter, backdrop-filter, display, content-visibility, overlay, pointer-events;");
        transition.Metadata.Css.ShouldContain(
            "transition-timing-function: var(--tw-ease, var(--default-transition-timing-function));");
        transition.Metadata.Css.ShouldContain(
            "transition-duration: var(--tw-duration, var(--default-transition-duration));");
        colors.Metadata!.Css.ShouldContain(
            "transition-property: color, background-color, border-color, outline-color, text-decoration-color, fill, stroke, --tw-gradient-from, --tw-gradient-via, --tw-gradient-to;");
        duration.Metadata!.Css.ShouldContain("--tw-duration: 150ms;");
        duration.Metadata.Css.ShouldContain("transition-duration: 150ms;");
        easing.Metadata!.Css.ShouldContain("--tw-ease: var(--ease-in-out);");
        initialEasing.Metadata!.Css.ShouldContain("--tw-ease: initial;");
        initialEasing.Metadata.Css.ShouldNotContain(
            "transition-timing-function:");

        var parsedTheme = UtilityThemeParser.Parse(
            """
            @theme {
              --transition-property-colors: transform;
              --transition-duration-slow: 2s;
            }
            """);
        parsedTheme.Diagnostics.ShouldBeEmpty();
        var customColors = UtilityCssRegistry.BuiltIn.Resolve(
            "transition-colors",
            parsedTheme.Theme,
            CancellationToken.None);
        var customDuration = UtilityCssRegistry.BuiltIn.Resolve(
            "duration-slow",
            parsedTheme.Theme,
            CancellationToken.None);
        customColors.Metadata!.Css.ShouldContain(
            "transition-property: var(--transition-property-colors);");
        customDuration.Metadata!.Css.ShouldContain(
            "--tw-duration: var(--transition-duration-slow);");

        var css = EmitDesignSystem(
            new[] { "transition", "duration-150", "ease-in-out" },
            UtilityTheme.Default);
        css.ShouldContain("@property --tw-duration {");
        css.ShouldContain("@property --tw-ease {");
    }

    [Fact]
    public void Resolve_TouchGestures_ComposeHorizontalVerticalAndPinchValues()
    {
        // Tailwind CSS v4.3.3 compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts#L1678-L1706
        var horizontal = UtilityCssRegistry.BuiltIn.Resolve("touch-pan-x");
        var vertical = UtilityCssRegistry.BuiltIn.Resolve("touch-pan-y");
        var pinch = UtilityCssRegistry.BuiltIn.Resolve("touch-pinch-zoom");
        const string composition =
            "touch-action: var(--tw-pan-x,) var(--tw-pan-y,) var(--tw-pinch-zoom,);";

        horizontal.Metadata!.Css.ShouldContain("--tw-pan-x: pan-x;");
        horizontal.Metadata.Css.ShouldContain(composition);
        vertical.Metadata!.Css.ShouldContain("--tw-pan-y: pan-y;");
        vertical.Metadata.Css.ShouldContain(composition);
        pinch.Metadata!.Css.ShouldContain("--tw-pinch-zoom: pinch-zoom;");
        pinch.Metadata.Css.ShouldContain(composition);

        var css = EmitDesignSystem(
            new[] { "touch-pan-x", "touch-pan-y", "touch-pinch-zoom" },
            UtilityTheme.Default);
        css.ShouldContain("@property --tw-pan-x {");
        css.ShouldContain("@property --tw-pan-y {");
        css.ShouldContain("@property --tw-pinch-zoom {");
    }

    [Fact]
    public void Resolve_LinearGradientInterpolation_EmitsLegacyBaseAndFeatureOverride()
    {
        // Tailwind CSS v4.3.3 compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.ts#L2588-L2604
        var result = UtilityCssRegistry.BuiltIn.Resolve(
            "bg-linear-to-r/oklab");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata!.Css.ShouldContain(
            "--tw-gradient-position: to right;");
        result.Metadata.Css.ShouldContain(
            "@supports (background-image: linear-gradient(in lab, red, red))");
        result.Metadata.Css.ShouldContain(
            "--tw-gradient-position: to right in oklab;");
        result.Metadata.Css.ShouldContain(
            "background-image: linear-gradient(var(--tw-gradient-stops));");
    }

    private static string EmitDesignSystem(
        string[] candidates,
        UtilityTheme theme)
    {
        var result = UtilityCssCompiler.Compile(
            candidates,
            UtilityCssRegistry.BuiltIn,
            theme,
            CancellationToken.None);
        result.Diagnostics.ShouldBeEmpty();
        return UtilityCssLayerEmitter.EmitDesignSystem(
            theme,
            result.Css,
            UtilityThemeOptions.Default,
            CancellationToken.None);
    }
}
