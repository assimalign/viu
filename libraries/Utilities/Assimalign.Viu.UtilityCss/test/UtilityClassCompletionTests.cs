using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilityClassCompletionTests
{
    [Fact]
    public void GetCompletions_BoundedQuery_ReturnsBudgetAndTruncationSignal()
    {
        var result = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                MaximumItems = 3,
            });

        result.Items.Count.ShouldBe(3);
        result.IsTruncated.ShouldBeTrue();
        result.Items.Select(item => item.SortOrder)
            .ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void GetCompletions_BaseOnlyQuery_ReturnsCompleteNamedBaseSurface()
    {
        var result = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                IncludeVariants = false,
                MaximumItems = 50000,
            });
        var candidateTexts = result.Items
            .Select(item => item.CandidateText)
            .ToHashSet(StringComparer.Ordinal);

        result.IsTruncated.ShouldBeFalse();
        candidateTexts.ShouldContain("m-0");
        candidateTexts.ShouldContain("m-0.5");
        candidateTexts.ShouldContain("m-96");
        candidateTexts.ShouldContain("m-auto");
        candidateTexts.ShouldContain("m-px");
        candidateTexts.ShouldContain("mx-auto");
        candidateTexts.ShouldContain("-m-4");
        candidateTexts.ShouldContain("-m-0");
        candidateTexts.ShouldContain("inset-auto");
        candidateTexts.ShouldContain("top-auto");
        candidateTexts.ShouldContain("inset-full");
        candidateTexts.ShouldContain("-top-full");
        candidateTexts.ShouldContain("w-auto");
        candidateTexts.ShouldContain("h-auto");
        candidateTexts.ShouldContain("size-auto");
        candidateTexts.ShouldContain("w-screen");
        candidateTexts.ShouldContain("h-dvh");
        candidateTexts.ShouldContain("grid-cols-12");
        candidateTexts.ShouldContain("col-span-12");
        candidateTexts.ShouldContain("object-top-left");
        candidateTexts.ShouldContain("object-bottom-right");
        candidateTexts.ShouldContain("flex-12");
        candidateTexts.ShouldContain("flex-1/2");
        candidateTexts.ShouldContain("order-12");
        candidateTexts.ShouldContain("z-20");
        candidateTexts.ShouldContain("border-4");
        candidateTexts.ShouldContain("border-x-4");
        candidateTexts.ShouldContain("opacity-25");
        candidateTexts.ShouldContain("font-stretch-50%");
        candidateTexts.ShouldContain("font-stretch-200%");
        candidateTexts.ShouldContain("grayscale-25");
        candidateTexts.ShouldContain("backdrop-invert-75");
        candidateTexts.ShouldContain("sepia-50");
        candidateTexts.ShouldContain("backdrop-sepia-50");
        candidateTexts.ShouldContain("bg-linear-330");
        candidateTexts.ShouldContain("-bg-linear-330");
        candidateTexts.ShouldContain("bg-conic-330");
        candidateTexts.ShouldContain("-bg-conic-330");
        candidateTexts.ShouldContain("-rotate-0");
        candidateTexts.ShouldContain("-rotate-45");
        candidateTexts.ShouldContain("-hue-rotate-90");
        candidateTexts.ShouldContain("-backdrop-hue-rotate-90");
        candidateTexts.ShouldContain("-translate-1/2");
        candidateTexts.ShouldContain("-scale-105");
        candidateTexts.ShouldNotContain("-m-auto");
        candidateTexts.ShouldNotContain("-inset-auto");
        candidateTexts.ShouldNotContain("-col-auto");
        candidateTexts.ShouldNotContain("-translate-none");
        candidateTexts.ShouldNotContain("-translate-screen");
        candidateTexts.ShouldNotContain("hover:m-4");
        candidateTexts.ShouldNotContain("sm:m-4");
    }

    [Fact]
    public void GetCompletions_ZeroBudget_ReportsWhetherMatchesExist()
    {
        var matching = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                Prefix = "bg-blue-",
                MaximumItems = 0,
            });
        var missing = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                Prefix = "not-a-utility-",
                MaximumItems = 0,
            });

        matching.Items.ShouldBeEmpty();
        matching.IsTruncated.ShouldBeTrue();
        missing.Items.ShouldBeEmpty();
        missing.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public void GetCompletions_NegativeBudget_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
                () => UtilityCssRegistry.BuiltIn.GetCompletions(
                    new UtilityClassCompletionQuery
                    {
                        MaximumItems = -1,
                    }))
            .ParamName.ShouldBe("MaximumItems");
    }

    [Theory]
    [InlineData("hover:bg-blue-", "hover:bg-blue-500")]
    [InlineData("dark:hover:bg-blue-", "dark:hover:bg-blue-500")]
    [InlineData("[&:focus]:bg-blue-", "[&:focus]:bg-blue-500")]
    public void GetCompletions_VariantPrefix_ComposesAndResolvesBuiltInCandidates(
        string prefix,
        string expectedCandidate)
    {
        var result = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                Prefix = prefix,
                MaximumItems = 100,
            });

        result.Items.ShouldContain(
            item => item.CandidateText == expectedCandidate);
        result.Items.ShouldAllBe(
            item => item.CandidateText.StartsWith(
                prefix,
                StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_ConfiguredPrefix_ComposesVariantChainWithoutDuplicatingPrefix()
    {
        var css = "@theme prefix(viu) { --color-brand: #123456; }";
        var theme = UtilityThemeParser.Parse(css).Theme;

        var result = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                Prefix = "viu:dark:hover:bg-brand",
                MaximumItems = 10,
            },
            theme);

        var completion = result.Items.ShouldHaveSingleItem();
        completion.CandidateText.ShouldBe("viu:dark:hover:bg-brand");
        completion.CandidateText.ShouldNotContain("viu:viu:");
        completion.ColorValue.ShouldBe("#123456");
    }

    [Fact]
    public void GetCompletions_ConfiguredPrefixAndEmptyQuery_ReturnsPrefixedCandidates()
    {
        var theme = UtilityThemeParser.Parse(
                "@theme prefix(viu) { --color-brand: #123456; }")
            .Theme;

        var result = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                MaximumItems = 2,
            },
            theme);

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(
            item => item.CandidateText.StartsWith(
                "viu:",
                StringComparison.Ordinal));
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public void GetCompletions_ConfiguredPrefixOnly_RetainsPreExpandedBreakpointCandidates()
    {
        var theme = UtilityThemeParser.Parse(
                "@theme prefix(viu) { --color-brand: #123456; }")
            .Theme;

        var result = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                Prefix = "viu:",
                MaximumItems = UtilityClassCompletionQuery.DefaultMaximumItems,
            },
            theme);

        result.Items.ShouldContain(
            item => item.CandidateText == "viu:sm:block");
        result.Items.Select(item => item.CandidateText)
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(result.Items.Count);
    }

    [Fact]
    public void GetCompletions_BreakpointPrefix_RetainsDirectAndComposedCandidateParity()
    {
        var result = UtilityCssRegistry.BuiltIn.GetCompletions(
            new UtilityClassCompletionQuery
            {
                Prefix = "sm:",
                MaximumItems = 100,
            });

        result.Items.ShouldContain(
            item => item.CandidateText == "sm:block");
        result.Items.Select(item => item.CandidateText)
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(result.Items.Count);
    }

    [Fact]
    public void GetCompletions_CanceledZeroBudget_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityCssRegistry.BuiltIn.GetCompletions(
                new UtilityClassCompletionQuery
                {
                    Prefix = "not-a-utility-",
                    MaximumItems = 0,
                },
                UtilityTheme.Default,
                cancellationSource.Token));
    }

    [Fact]
    public void Resolve_ColorUtility_ExposesResolvedThemeColorWithoutCssScanning()
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve("bg-blue-500");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.ColorValue.ShouldBe("oklch(62.3% 0.214 259.815)");
    }

    [Fact]
    public void Resolve_NonColorUtilityContainingInlineColorText_DoesNotReportFalseColor()
    {
        var theme = UtilityThemeParser.Parse(
                "@theme inline { --color-decoy: block; }")
            .Theme;

        var result = UtilityCssRegistry.BuiltIn.Resolve(
            "block",
            theme,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Css.ShouldContain("display: block;");
        result.Metadata.ColorValue.ShouldBeNull();
    }

    [Fact]
    public void Resolve_NonColorUtility_HasNoColorValue()
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve("gap-4");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.ColorValue.ShouldBeNull();
    }
}
