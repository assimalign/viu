using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityGradientMaskTests
{
    [Theory]
    [InlineData(
        "from-red-500",
        "--tw-gradient-from: var(--color-red-500);",
        "--tw-gradient-stops: var(--tw-gradient-via-stops, var(--tw-gradient-position),")]
    [InlineData(
        "from-25%",
        "--tw-gradient-from-position: 25%;",
        null)]
    [InlineData(
        "via-blue-500/50",
        "--tw-gradient-via: color-mix(in oklab, var(--color-blue-500) 50%, transparent);",
        "--tw-gradient-stops: var(--tw-gradient-via-stops);")]
    [InlineData(
        "to-transparent",
        "--tw-gradient-to: transparent;",
        "--tw-gradient-stops: var(--tw-gradient-via-stops, var(--tw-gradient-position),")]
    [InlineData(
        "via-[50px]",
        "--tw-gradient-via-position: 50px;",
        null)]
    [InlineData(
        "to-[color:var(--brand)]/[0.5]",
        "--tw-gradient-to: color-mix(in oklab, var(--brand) calc(0.5 * 100%), transparent);",
        null)]
    public void Resolve_GradientStopFamily_EmitsTaggedV433StopSemantics(
        string candidate,
        string expected,
        string? additionalExpected)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        result.Metadata!.Css.ShouldContain(expected);
        if (additionalExpected is not null)
        {
            result.Metadata.Css.ShouldContain(additionalExpected);
        }
    }

    [Theory]
    [InlineData(
        "bg-linear-to-r/oklab",
        "--tw-gradient-position: to right in oklab;",
        "background-image: linear-gradient(var(--tw-gradient-stops));")]
    [InlineData(
        "bg-conic-90/longer",
        "--tw-gradient-position: from 90deg in oklch longer hue;",
        "background-image: conic-gradient(var(--tw-gradient-stops));")]
    [InlineData(
        "bg-radial/oklch",
        "--tw-gradient-position: in oklch;",
        "background-image: radial-gradient(var(--tw-gradient-stops));")]
    public void Resolve_GradientInterpolationModifier_UsesTaggedV433Method(
        string candidate,
        string expectedPosition,
        string expectedImage)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        result.Metadata!.Css.ShouldContain(expectedPosition);
        result.Metadata.Css.ShouldContain(expectedImage);
    }

    [Theory]
    [InlineData("bg-[120px]", "background-position: 120px;")]
    [InlineData("bg-[50%]", "background-position: 50%;")]
    [InlineData("bg-[cover]", "background-size: cover;")]
    [InlineData("bg-[url:var(--art)]", "background-image: var(--art);")]
    public void Resolve_ArbitraryBackground_InfersTaggedV433Property(
        string candidate,
        string expected)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        result.Metadata!.Css.ShouldContain(expected);
        result.Metadata.Css.ShouldNotContain("background-color:");
    }

    [Fact]
    public void Resolve_MaskEdgeStop_UpdatesEverySelectedEdge()
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve("mask-x-from-20");

        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        result.Metadata!.Css.ShouldContain(
            "mask-image: var(--tw-mask-linear), var(--tw-mask-radial), var(--tw-mask-conic);");
        result.Metadata.Css.ShouldContain("mask-composite: intersect;");
        result.Metadata.Css.ShouldContain(
            "--tw-mask-linear: var(--tw-mask-left), var(--tw-mask-right), var(--tw-mask-bottom), var(--tw-mask-top);");
        result.Metadata.Css.ShouldContain(
            "--tw-mask-right-from-position: calc(var(--spacing) * 20);");
        result.Metadata.Css.ShouldContain(
            "--tw-mask-left-from-position: calc(var(--spacing) * 20);");
    }

    [Theory]
    [InlineData(
        "mask-linear-from-black",
        "--tw-mask-linear-stops: var(--tw-mask-linear-position),",
        "--tw-mask-linear-from-color: var(--color-black);")]
    [InlineData(
        "mask-radial-from-20",
        "--tw-mask-radial-stops: var(--tw-mask-radial-shape) var(--tw-mask-radial-size)",
        "--tw-mask-radial-from-position: calc(var(--spacing) * 20);")]
    [InlineData(
        "mask-conic-to-[75%]",
        "--tw-mask-conic-stops: from var(--tw-mask-conic-position),",
        "--tw-mask-conic-to-position: 75%;")]
    [InlineData(
        "mask-linear-to-[color:var(--fade)]/50",
        "--tw-mask-linear-to-color: color-mix(in oklab, var(--fade) 50%, transparent);",
        "--tw-mask-linear: linear-gradient(var(--tw-mask-linear-stops));")]
    public void Resolve_MaskGeometryStop_EmitsTaggedV433Composition(
        string candidate,
        string expectedComposition,
        string expectedStop)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        result.Metadata!.Css.ShouldContain(expectedComposition);
        result.Metadata.Css.ShouldContain(expectedStop);
    }

    [Theory]
    [InlineData(
        "mask-linear-45",
        "--tw-mask-linear: linear-gradient(var(--tw-mask-linear-stops, var(--tw-mask-linear-position)));",
        "--tw-mask-linear-position: calc(1deg * 45);")]
    [InlineData(
        "-mask-conic-1",
        "--tw-mask-conic: conic-gradient(var(--tw-mask-conic-stops, var(--tw-mask-conic-position)));",
        "--tw-mask-conic-position: -1deg;")]
    [InlineData(
        "mask-radial-[25%_25%]",
        "--tw-mask-radial: radial-gradient(var(--tw-mask-radial-stops, var(--tw-mask-radial-size)));",
        "--tw-mask-radial-size: 25% 25%;")]
    [InlineData(
        "mask-radial-at-top",
        "--tw-mask-radial-position: top;",
        null)]
    public void Resolve_MaskGeometry_EmitsComposableTaggedV433Variables(
        string candidate,
        string expected,
        string? additionalExpected)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        result.Metadata!.Css.ShouldContain(expected);
        if (additionalExpected is not null)
        {
            result.Metadata.Css.ShouldContain(additionalExpected);
            result.Metadata.Css.ShouldContain(
                "mask-image: var(--tw-mask-linear), var(--tw-mask-radial), var(--tw-mask-conic);");
            result.Metadata.Css.ShouldContain("mask-composite: intersect;");
        }
        else
        {
            result.Metadata.Css.ShouldNotContain("mask-image:");
        }
    }

    [Theory]
    [InlineData("mask-linear")]
    [InlineData("mask-radial")]
    [InlineData("mask-conic")]
    [InlineData("mask-radial-at-unknown")]
    [InlineData("mask-linear-from-2.8175")]
    [InlineData("mask-radial-to-[-25%]")]
    public void Resolve_InvalidMaskGeometryAndStops_AreRejected(
        string candidate)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeFalse();
        result.Metadata.ShouldBeNull();
    }

    [Theory]
    [InlineData(
        "translate-z-px",
        "--tw-translate-z: 1px;",
        "translate: var(--tw-translate-x) var(--tw-translate-y) var(--tw-translate-z);")]
    [InlineData(
        "rotate-z-45",
        "--tw-rotate-z: rotateZ(45deg);",
        "transform: var(--tw-rotate-x,) var(--tw-rotate-y,) var(--tw-rotate-z,) var(--tw-skew-x,) var(--tw-skew-y,);")]
    [InlineData(
        "scale-z-50",
        "--tw-scale-z: 50%;",
        "scale: var(--tw-scale-x) var(--tw-scale-y) var(--tw-scale-z);")]
    public void Resolve_DepthTransform_EmitsTaggedV433Composition(
        string candidate,
        string expectedVariable,
        string expectedComposition)
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve(candidate);

        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
        result.Metadata!.Css.ShouldContain(expectedVariable);
        result.Metadata.Css.ShouldContain(expectedComposition);
    }

    [Fact]
    public void Registry_NewV433Roots_AreExecutableAndAdvertised()
    {
        var roots = UtilityCssRegistry.BuiltIn.Definitions
            .Select(definition => definition.Root)
            .ToArray();

        roots.ShouldContain("from");
        roots.ShouldContain("via");
        roots.ShouldContain("to");
        roots.ShouldContain("mask-x-from");
        roots.ShouldContain("mask-linear-to");
        roots.ShouldContain("mask-radial-from");
        roots.ShouldContain("mask-conic-to");
        roots.ShouldContain("translate-z");
        roots.ShouldContain("rotate-z");
        roots.ShouldContain("scale-z");
        roots.ShouldNotContain("mask-linear-via");
    }
}
