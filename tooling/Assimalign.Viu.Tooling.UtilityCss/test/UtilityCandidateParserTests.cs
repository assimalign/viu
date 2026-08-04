using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCandidateParserTests
{
    [Fact]
    public void Parse_StackedVariantModifierAndImportant_PreservesV4Structure()
    {
        // Tailwind CSS v4.3.3 compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/candidate.test.ts
        var candidate = ParseCandidate("hover:focus:bg-blue-500/50!");

        candidate.Root.ShouldBe("bg");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Kind.ShouldBe(UtilityValueKind.Named);
        candidate.Value.Text.ShouldBe("blue-500");
        candidate.Value.Fraction.ShouldBe("blue-500/50");
        candidate.Modifier.ShouldNotBeNull();
        candidate.Modifier.Kind.ShouldBe(UtilityModifierKind.Named);
        candidate.Modifier.Text.ShouldBe("50");
        candidate.ImportantMarker.ShouldBe(UtilityImportantMarker.Trailing);
        candidate.IsImportant.ShouldBeTrue();
        candidate.CanonicalText.ShouldBe("hover:focus:bg-blue-500/50!");
        candidate.Variants.Select(variant => variant.Root)
            .ShouldBe(new[] { "hover", "focus" });
        candidate.Variants.Select(variant => variant.SourceOrder)
            .ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public void Parse_DeprecatedLeadingImportant_ReturnsCandidateWarningAndCanonicalTrailingText()
    {
        var result = UtilityCandidateParser.Parse("hover:!mt-4");

        result.IsSuccess.ShouldBeTrue();
        result.Candidate.ShouldNotBeNull();
        result.Candidate.ImportantMarker.ShouldBe(UtilityImportantMarker.DeprecatedLeading);
        result.Candidate.UsesDeprecatedImportantMarker.ShouldBeTrue();
        result.Candidate.CanonicalText.ShouldBe("hover:mt-4!");
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Code.ShouldBe(
            UtilityCandidateDiagnosticCode.DeprecatedLeadingImportantMarker);
        result.Diagnostics[0].Severity.ShouldBe(UtilityCandidateDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Parse_NegativeUtility_SeparatesNegativeFormFromPositiveRoot()
    {
        var candidate = ParseCandidate("-translate-x-4");

        candidate.IsNegative.ShouldBeTrue();
        candidate.Root.ShouldBe("translate-x");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Text.ShouldBe("4");
    }

    [Fact]
    public void Parse_NamedFraction_PreservesValueModifierAndFraction()
    {
        var candidate = ParseCandidate("w-1/2");

        candidate.Root.ShouldBe("w");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Text.ShouldBe("1");
        candidate.Value.Fraction.ShouldBe("1/2");
        candidate.Modifier.ShouldNotBeNull();
        candidate.Modifier.Text.ShouldBe("2");
    }

    [Fact]
    public void Parse_ArbitraryModifier_DoesNotAssumeFractionCapability()
    {
        var candidate = ParseCandidate("bg-red-500/[50%]");

        candidate.Value.ShouldNotBeNull();
        candidate.Value.Text.ShouldBe("red-500");
        candidate.Value.Fraction.ShouldBeNull();
        candidate.Modifier.ShouldNotBeNull();
        candidate.Modifier.Kind.ShouldBe(UtilityModifierKind.Arbitrary);
        candidate.Modifier.Text.ShouldBe("50%");
    }

    [Fact]
    public void Parse_ArbitraryValue_DecodesUnderscoresAndEscapedUnderscores()
    {
        var candidate = ParseCandidate(@"grid-cols-[1fr_2fr_var(--named_value)_literal\_underscore]");

        candidate.Root.ShouldBe("grid-cols");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Kind.ShouldBe(UtilityValueKind.Arbitrary);
        candidate.Value.Text.ShouldBe("1fr 2fr var(--named_value) literal_underscore");
        candidate.Value.RawText.ShouldBe(@"[1fr_2fr_var(--named_value)_literal\_underscore]");
    }

    [Fact]
    public void Parse_TypedArbitraryValue_PreservesTypeHint()
    {
        var candidate = ParseCandidate("bg-[color:var(--brand)]");

        candidate.Root.ShouldBe("bg");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Kind.ShouldBe(UtilityValueKind.Arbitrary);
        candidate.Value.DataType.ShouldBe("color");
        candidate.Value.Text.ShouldBe("var(--brand)");
    }

    [Fact]
    public void Parse_CssVariableShorthand_PreservesVariableAndTypeHint()
    {
        var candidate = ParseCandidate("bg-(color:--brand_color,#0088cc)");

        candidate.Root.ShouldBe("bg");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Kind.ShouldBe(UtilityValueKind.CssVariable);
        candidate.Value.DataType.ShouldBe("color");
        candidate.Value.Text.ShouldBe("var(--brand_color,#0088cc)");
    }

    [Fact]
    public void Parse_CssVariableModifier_PreservesVariableShorthand()
    {
        var candidate = ParseCandidate("bg-red-500/(--brand_opacity)");

        candidate.Modifier.ShouldNotBeNull();
        candidate.Modifier.Kind.ShouldBe(UtilityModifierKind.CssVariable);
        candidate.Modifier.Text.ShouldBe("var(--brand_opacity)");
    }

    [Fact]
    public void Parse_ArbitraryProperty_PreservesPropertyValueAndModifier()
    {
        var candidate = ParseCandidate("[mask-type:luminance]/50!");

        candidate.Kind.ShouldBe(UtilityCandidateKind.ArbitraryProperty);
        candidate.Root.ShouldBe("mask-type");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Kind.ShouldBe(UtilityValueKind.Arbitrary);
        candidate.Value.Text.ShouldBe("luminance");
        candidate.Modifier.ShouldNotBeNull();
        candidate.Modifier.Text.ShouldBe("50");
        candidate.IsImportant.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Prefix_RepresentsPrefixAsFirstVariant()
    {
        var candidate = ParseCandidate("viu:hover:focus:bg-red-500", "viu");

        candidate.Variants.Count.ShouldBe(3);
        candidate.Variants[0].Kind.ShouldBe(UtilityVariantKind.Prefix);
        candidate.Variants[0].Category.ShouldBe(UtilityVariantCategory.Prefix);
        candidate.Variants[0].Root.ShouldBe("viu");
        candidate.Variants.Select(variant => variant.SourceOrder)
            .ShouldBe(new[] { 0, 1, 2 });
        candidate.Variants.Select(variant => variant.Root)
            .ShouldBe(new[] { "viu", "hover", "focus" });
    }

    [Fact]
    public void Parse_MissingConfiguredPrefix_ReturnsRecoverableError()
    {
        var result = UtilityCandidateParser.Parse("hover:bg-red-500", "viu");

        result.IsSuccess.ShouldBeFalse();
        result.Candidate.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Code.ShouldBe(UtilityCandidateDiagnosticCode.PrefixMismatch);
    }

    [Fact]
    public void Parse_ArbitrarySelectorVariant_PreservesSelectorShape()
    {
        var candidate = ParseCandidate("[&:nth-child(3)]:underline");
        var variant = candidate.Variants.ShouldHaveSingleItem();

        variant.Kind.ShouldBe(UtilityVariantKind.Arbitrary);
        variant.Category.ShouldBe(UtilityVariantCategory.Arbitrary);
        variant.Selector.ShouldBe("&:nth-child(3)");
        variant.IsRelativeSelector.ShouldBeFalse();
    }

    [Fact]
    public void Parse_BareArbitrarySelector_NormalizesSelector()
    {
        var candidate = ParseCandidate("[p]:underline");
        var variant = candidate.Variants.ShouldHaveSingleItem();

        variant.Selector.ShouldBe("&:is(p)");
        variant.IsRelativeSelector.ShouldBeFalse();
    }

    [Fact]
    public void Parse_RelativeArbitrarySelector_PreservesRelativeMarker()
    {
        var candidate = ParseCandidate("[>img]:block");
        var variant = candidate.Variants.ShouldHaveSingleItem();

        variant.Selector.ShouldBe(">img");
        variant.IsRelativeSelector.ShouldBeTrue();
    }

    [Fact]
    public void Parse_CompoundVariant_PreservesNestedVariantAndNamedModifier()
    {
        var candidate = ParseCandidate("group-data-[state=open]/card:bg-red-500");
        var group = candidate.Variants.ShouldHaveSingleItem();

        group.Kind.ShouldBe(UtilityVariantKind.Compound);
        group.Root.ShouldBe("group");
        group.Modifier.ShouldNotBeNull();
        group.Modifier.Text.ShouldBe("card");
        group.NestedVariant.ShouldNotBeNull();
        group.NestedVariant.Kind.ShouldBe(UtilityVariantKind.Functional);
        group.NestedVariant.Root.ShouldBe("data");
        group.NestedVariant.Value.ShouldNotBeNull();
        group.NestedVariant.Value.Kind.ShouldBe(UtilityValueKind.Arbitrary);
        group.NestedVariant.Value.Text.ShouldBe("state=open");
        group.NestedVariant.SourceOrder.ShouldBe(0);
    }

    [Fact]
    public void Parse_NestedCompoundVariant_PreservesEveryLayer()
    {
        var candidate = ParseCandidate("not-group-hover/card:flex");
        var notVariant = candidate.Variants.ShouldHaveSingleItem();

        notVariant.Root.ShouldBe("not");
        notVariant.NestedVariant.ShouldNotBeNull();
        notVariant.NestedVariant.Root.ShouldBe("group");
        notVariant.NestedVariant.Modifier.ShouldNotBeNull();
        notVariant.NestedVariant.Modifier.Text.ShouldBe("card");
        notVariant.NestedVariant.NestedVariant.ShouldNotBeNull();
        notVariant.NestedVariant.NestedVariant.Root.ShouldBe("hover");
    }

    [Fact]
    public void Parse_FunctionalCssVariableVariant_PreservesShorthand()
    {
        var candidate = ParseCandidate("supports-(--display_mode):flex");
        var variant = candidate.Variants.ShouldHaveSingleItem();

        variant.Kind.ShouldBe(UtilityVariantKind.Functional);
        variant.Root.ShouldBe("supports");
        variant.Value.ShouldNotBeNull();
        variant.Value.Kind.ShouldBe(UtilityValueKind.CssVariable);
        variant.Value.Text.ShouldBe("var(--display_mode)");
    }

    [Theory]
    [InlineData("sm", UtilityVariantCategory.Responsive)]
    [InlineData("max-[900px]", UtilityVariantCategory.Responsive)]
    [InlineData("@md", UtilityVariantCategory.ContainerQuery)]
    [InlineData("@max-lg/sidebar", UtilityVariantCategory.ContainerQuery)]
    [InlineData("aria-disabled", UtilityVariantCategory.Attribute)]
    [InlineData("data-[state=open]", UtilityVariantCategory.Attribute)]
    [InlineData("supports-[display:grid]", UtilityVariantCategory.Supports)]
    [InlineData("motion-safe", UtilityVariantCategory.Environment)]
    [InlineData("rtl", UtilityVariantCategory.Direction)]
    [InlineData("print", UtilityVariantCategory.Print)]
    [InlineData("*", UtilityVariantCategory.Child)]
    [InlineData("**", UtilityVariantCategory.Descendant)]
    public void Parse_BuiltInVariantSurface_ClassifiesFamily(
        string variantText,
        UtilityVariantCategory expectedCategory)
    {
        var candidate = ParseCandidate(variantText + ":flex");

        candidate.Variants.ShouldHaveSingleItem().Category.ShouldBe(expectedCategory);
    }

    [Fact]
    public void Parse_NestedDelimitersQuotesAndEscapes_SplitsOnlyAtTopLevel()
    {
        var candidate = ParseCandidate(
            @"supports-[selector(:has(a[href='x:y']))]:[&:is(.a\:b)]:bg-[url(""data:image/svg+xml;foo:bar_baz"")]");

        candidate.Variants.Count.ShouldBe(2);
        candidate.Variants[0].Root.ShouldBe("supports");
        candidate.Variants[0].Value.ShouldNotBeNull();
        candidate.Variants[0].Value!.Text.ShouldBe("selector(:has(a[href='x:y']))");
        candidate.Variants[1].Selector.ShouldBe(@"&:is(.a\:b)");
        candidate.Value.ShouldNotBeNull();
        candidate.Value.Text.ShouldBe(@"url(""data:image/svg+xml;foo:bar_baz"")");
    }

    [Theory]
    [InlineData("")]
    [InlineData(":flex")]
    [InlineData("hover:")]
    [InlineData("bg-[#0088cc")]
    [InlineData("bg-red-500/50/25")]
    [InlineData("bg-[]")]
    [InlineData("bg-()")]
    [InlineData("[color:]")]
    [InlineData("-")]
    [InlineData("unknown:flex")]
    [InlineData("hover-foo:flex")]
    [InlineData("group:flex")]
    [InlineData("supports-[]:flex")]
    [InlineData("[@media(width>=123px){&:hover}]:flex")]
    [InlineData("data-[foo_^_=_'bar']:flex")]
    [InlineData("group-[@media_foo]:flex")]
    [InlineData("group-not-hover:flex")]
    [InlineData("not-*:flex")]
    [InlineData("peer-[@media_foo]:flex")]
    [InlineData("!mt-4!")]
    [InlineData(@"hover\:flex")]
    [InlineData(@"bg-[value\")]
    public void Parse_MalformedAuthorInput_ReturnsDiagnosticsWithoutThrowing(string rawText)
    {
        UtilityCandidateParseResult? result = null;

        Should.NotThrow(() => result = UtilityCandidateParser.Parse(rawText));

        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Candidate.ShouldBeNull();
        result.Diagnostics.Count.ShouldBeGreaterThan(0);
        result.Diagnostics.ShouldAllBe(
            diagnostic => diagnostic.Severity == UtilityCandidateDiagnosticSeverity.Error);
    }

    [Fact]
    public void Parse_CompoundVariantWithCompatibleNestedRule_RemainsValid()
    {
        var candidate = ParseCandidate("group-not-disabled:flex");

        candidate.Variants.ShouldHaveSingleItem().Root.ShouldBe("group");
    }

    [Fact]
    public void Parse_NullAuthorInput_ReturnsEmptyCandidateDiagnostic()
    {
        var result = UtilityCandidateParser.Parse(null);

        result.IsSuccess.ShouldBeFalse();
        result.Candidate.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(
            UtilityCandidateDiagnosticCode.EmptyCandidate);
    }

    [Fact]
    public void Parse_CanceledToken_ThrowsOnlyCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityCandidateParser.Parse(
                "hover:flex",
                null,
                UtilityVariantRegistry.BuiltIn,
                source.Token));
    }

    [Fact]
    public void Parse_EquivalentInput_ProducesEqualCandidatesAndHashes()
    {
        var first = UtilityCandidateParser.Parse("hover:focus:bg-red-500/50!");
        var second = UtilityCandidateParser.Parse("hover:focus:bg-red-500/50!");
        var reordered = UtilityCandidateParser.Parse("focus:hover:bg-red-500/50!");

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Candidate.ShouldNotBeNull();
        reordered.Candidate.ShouldNotBeNull();
        first.Candidate.ShouldNotBe(reordered.Candidate);
        first.Candidate.Variants.ShouldNotBe(reordered.Candidate.Variants);
    }

    private static UtilityCandidate ParseCandidate(
        string rawText,
        string? prefix = null)
    {
        var result = UtilityCandidateParser.Parse(rawText, prefix);

        result.IsSuccess.ShouldBeTrue();
        result.Candidate.ShouldNotBeNull();
        return result.Candidate;
    }
}
