using System;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCssV433ShadowAndValidationTests
{
    [Fact]
    public void Compile_ShadowSizeAndColorUtilities_ComposeThroughNonInheritedVariables()
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "shadow-sm",
                "shadow-red-500",
                "inset-shadow-sm",
                "inset-shadow-red-500",
                "text-shadow-sm",
                "text-shadow-red-500",
                "drop-shadow-sm",
                "drop-shadow-red-500",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Css.ShouldContain(
            "--tw-shadow:");
        result.Css.ShouldContain(
            "var(--tw-shadow-color,");
        result.Css.ShouldContain(
            "--tw-shadow-color: color-mix(in oklab, var(--color-red-500) var(--tw-shadow-alpha), transparent);");
        result.Css.ShouldContain(
            "--tw-inset-shadow:");
        result.Css.ShouldContain(
            "var(--tw-inset-shadow-color,");
        result.Css.ShouldContain(
            "--tw-text-shadow-color: color-mix(in oklab, var(--color-red-500) var(--tw-text-shadow-alpha), transparent);");
        result.Css.ShouldContain(
            "--tw-drop-shadow-size: drop-shadow(");
        result.Css.ShouldContain(
            "var(--tw-drop-shadow-color,");
        result.Css.ShouldContain(
            "--tw-drop-shadow: var(--tw-drop-shadow-size);");
        result.Css.ShouldContain(
            "box-shadow: var(--tw-inset-shadow), var(--tw-inset-ring-shadow), var(--tw-ring-offset-shadow), var(--tw-ring-shadow), var(--tw-shadow);");
    }

    [Fact]
    public void Compile_FractionalShadowOpacity_UsesValidatedAlphaVariables()
    {
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "shadow-sm/12.5",
                "inset-shadow-sm/12.5",
                "text-shadow-sm/12.5",
                "drop-shadow-sm/12.5",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Css.ShouldContain("--tw-shadow-alpha: 12.5%;");
        result.Css.ShouldContain("--tw-inset-shadow-alpha: 12.5%;");
        result.Css.ShouldContain("--tw-text-shadow-alpha: 12.5%;");
        result.Css.ShouldContain("--tw-drop-shadow-alpha: 12.5%;");
        result.Css.ShouldContain(
            "var(--tw-shadow-color, oklab(from");
        result.Css.ShouldContain(
            "var(--tw-drop-shadow-color, oklab(from");
    }

    [Theory]
    [InlineData("absolute/foo")]
    [InlineData("flex/foo")]
    [InlineData("grid-cols-12/foo")]
    [InlineData("-order-first")]
    [InlineData("rotate--2")]
    [InlineData("px-.75")]
    [InlineData("opacity-1.125")]
    [InlineData("outline/foo")]
    [InlineData("table-auto/foo")]
    public void Compile_InvalidV433Candidate_EmitsNoRule(
        string candidate)
    {
        // Compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var result = UtilityCssCompiler.Compile(
            new[] { candidate });

        result.Rules.ShouldBeEmpty();
        result.Diagnostics.ShouldNotBeEmpty();
        result.Css.ShouldNotContain(
            "." + candidate,
            Case.Insensitive);
    }

    [Fact]
    public void Compile_ValidQuarterStepOpacity_EmitsRule()
    {
        var result = UtilityCssCompiler.Compile(
            new[] { "opacity-1.25" });

        result.Diagnostics.ShouldBeEmpty();
        result.Css.ShouldContain("opacity: 0.0125;");
    }

    [Fact]
    public void Compile_PrefixedAndComposedCompatibilityDeclarations_EmitOfficialForms()
    {
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "backdrop-blur-sm",
                "blur-none",
                "border-2",
                "border-dashed",
                "box-decoration-clone",
                "box-decoration-slice",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Css.ShouldContain("-webkit-backdrop-filter:");
        result.Css.ShouldContain("backdrop-filter:");
        result.Css.ShouldNotContain("--tw-backdrop-drop-shadow");
        result.Css.ShouldContain("--tw-blur: ;");
        result.Css.ShouldContain("--tw-border-style: dashed;");
        result.Css.ShouldContain(
            "border-style: var(--tw-border-style);");
        result.Css.ShouldContain("-webkit-box-decoration-break: clone;");
        result.Css.ShouldContain("-webkit-box-decoration-break: slice;");
    }

    [Fact]
    public void Compile_AxisTransforms_ComposeThroughTypedVariables()
    {
        var result = UtilityCssCompiler.Compile(
            new[]
            {
                "translate-x-1",
                "translate-y-2",
                "rotate-x-45",
                "rotate-y-30",
                "scale-x-50",
                "scale-y-75",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Css.ShouldContain("--tw-translate-x:");
        result.Css.ShouldContain("--tw-translate-y:");
        result.Css.ShouldContain(
            "translate: var(--tw-translate-x) var(--tw-translate-y);");
        result.Css.ShouldContain("--tw-rotate-x: rotateX(45deg);");
        result.Css.ShouldContain("--tw-rotate-y: rotateY(30deg);");
        result.Css.ShouldContain(
            "transform: var(--tw-rotate-x,) var(--tw-rotate-y,) var(--tw-rotate-z,)");
        result.Css.ShouldContain("--tw-scale-x: 0.5;");
        result.Css.ShouldContain("--tw-scale-y: 0.75;");
        result.Css.ShouldContain(
            "scale: var(--tw-scale-x) var(--tw-scale-y);");
    }
}
