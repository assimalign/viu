using System;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilityProjectEditorApiTests
{
    [Fact]
    public void Resolve_BuiltInCandidate_ReturnsGeneratedDeclarationsInOneCall()
    {
        var result = UtilityProjectStylesheetCompiler.Resolve(
            string.Empty,
            "hover:bg-blue-500");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Css.ShouldContain("background-color: var(--color-blue-500);");
        result.Metadata.Css.ShouldContain(":hover");
        result.Metadata.ColorValue.ShouldBe("oklch(62.3% 0.214 259.815)");
    }

    [Fact]
    public void Resolve_ProjectUtility_ReturnsGeneratedDeclarationsWithoutBuiltInMiss()
    {
        var result = UtilityProjectStylesheetCompiler.Resolve(
            "@utility brand-surface { background-color: rebeccapurple; }",
            "brand-surface");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Css.ShouldContain("background-color: rebeccapurple;");
        result.Metadata.ColorValue.ShouldBe("rebeccapurple");
        result.UtilityDiagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_ProjectUtilityWithDistinctColorDeclarations_HasNoAmbiguousColorValue()
    {
        var result = UtilityProjectStylesheetCompiler.Resolve(
            "@utility two-colors { color: red; background-color: blue; }",
            "two-colors");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Css.ShouldContain("color: red;");
        result.Metadata.Css.ShouldContain("background-color: blue;");
        result.Metadata.ColorValue.ShouldBeNull();
    }

    [Fact]
    public void Resolve_ProjectVariantAroundBuiltIn_PreservesColorMetadata()
    {
        var result = UtilityProjectStylesheetCompiler.Resolve(
            "@custom-variant theme-midnight (&:where([data-theme=midnight] *));",
            "theme-midnight:hover:bg-blue-500");

        result.IsSuccess.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Css.ShouldContain("&:where([data-theme=midnight] *)");
        result.Metadata.Css.ShouldContain("&:hover");
        result.Metadata.ColorValue.ShouldBe("oklch(62.3% 0.214 259.815)");
        result.UtilityDiagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_UnknownCandidate_ReturnsRecoverableRegistryDiagnostic()
    {
        var result = UtilityProjectStylesheetCompiler.Resolve(
            string.Empty,
            "not-a-utility");

        result.IsSuccess.ShouldBeFalse();
        result.Metadata.ShouldBeNull();
        result.UtilityDiagnostics.ShouldHaveSingleItem().Code.ShouldBe(
            UtilityCssDiagnosticCode.UnsupportedUtility);
    }

    [Fact]
    public void GetCompletions_StaticProjectUtilityAndBuiltInVariant_AreComposedTogether()
    {
        var result = UtilityProjectStylesheetCompiler.GetCompletions(
            "@utility brand-surface { background-color: rebeccapurple; }",
            new UtilityClassCompletionQuery
            {
                Prefix = "hover:brand-",
                MaximumItems = 20,
            });

        var completion = result.Items.ShouldHaveSingleItem();
        completion.CandidateText.ShouldBe("hover:brand-surface");
        completion.Css.ShouldContain("&:hover");
        completion.ColorValue.ShouldBe("rebeccapurple");
    }

    [Fact]
    public void GetCompletions_BaseOnlyQueryWithVariantPrefix_ExcludesProjectUtility()
    {
        var result = UtilityProjectStylesheetCompiler.GetCompletions(
            "@utility brand-surface { background-color: rebeccapurple; }",
            new UtilityClassCompletionQuery
            {
                Prefix = "hover:brand-",
                IncludeVariants = false,
                MaximumItems = 20,
            });

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_BaseOnlyQueryWithConfiguredPrefix_IncludesProjectUtility()
    {
        const string Css =
            "@theme prefix(viu) { --color-brand: #123456; }\n" +
            "@utility brand-surface { background-color: var(--viu-color-brand); }";
        var options = new UtilityProjectStylesheetCompilationOptions
        {
            Theme = UtilityThemeParser.Parse(Css).Theme,
        };

        var result = UtilityProjectStylesheetCompiler.GetCompletions(
            Css,
            new UtilityClassCompletionQuery
            {
                Prefix = "viu:brand-",
                IncludeVariants = false,
                MaximumItems = 20,
            },
            options);

        result.Items.ShouldHaveSingleItem().CandidateText.ShouldBe(
            "viu:brand-surface");
    }

    [Fact]
    public void GetCompletions_ProjectAndBuiltInVariants_ComposeBuiltInUtilities()
    {
        var result = UtilityProjectStylesheetCompiler.GetCompletions(
            "@custom-variant theme-midnight (&:where([data-theme=midnight] *));",
            new UtilityClassCompletionQuery
            {
                Prefix = "theme-midnight:hover:bg-blue-",
                MaximumItems = 100,
            });

        var completion = result.Items.Single(
            item => item.CandidateText == "theme-midnight:hover:bg-blue-500");
        completion.Css.ShouldContain("&:where([data-theme=midnight] *)");
        completion.Css.ShouldContain("&:hover");
        completion.ColorValue.ShouldBe("oklch(62.3% 0.214 259.815)");
    }

    [Fact]
    public void GetCompletions_FunctionalProjectUtilityWithDefault_OffersResolvableStem()
    {
        var result = UtilityProjectStylesheetCompiler.GetCompletions(
            "@utility tab-* { tab-size: --value(integer, --default(4)); }",
            new UtilityClassCompletionQuery
            {
                Prefix = "tab",
                MaximumItems = 20,
            });

        var completion = result.Items.Single(
            item => item.CandidateText == "tab");
        completion.Css.ShouldContain("tab-size: 4;");
    }

    [Fact]
    public void GetCompletions_ConfiguredPrefix_ComposesBuiltInAndProjectCandidates()
    {
        var css =
            "@theme prefix(viu) { --color-brand: #123456; }\n" +
            "@utility brand-surface { background-color: var(--viu-color-brand); }\n" +
            "@custom-variant theme-midnight (&:where([data-theme=midnight] *));";
        var options = new UtilityProjectStylesheetCompilationOptions
        {
            Theme = UtilityThemeParser.Parse(css).Theme,
        };

        var project = UtilityProjectStylesheetCompiler.GetCompletions(
            css,
            new UtilityClassCompletionQuery
            {
                Prefix = "viu:theme-midnight:hover:brand-",
                MaximumItems = 20,
            },
            options);
        var builtIn = UtilityProjectStylesheetCompiler.GetCompletions(
            css,
            new UtilityClassCompletionQuery
            {
                Prefix = "viu:dark:hover:bg-brand",
                MaximumItems = 20,
            },
            options);

        var projectCompletion = project.Items.ShouldHaveSingleItem();
        projectCompletion.CandidateText.ShouldBe(
            "viu:theme-midnight:hover:brand-surface");
        projectCompletion.ColorValue.ShouldBe("#123456");
        builtIn.Items.ShouldHaveSingleItem().CandidateText.ShouldBe(
            "viu:dark:hover:bg-brand");
    }

    [Fact]
    public void GetCompletions_PartialConfiguredPrefix_IncludesProjectCandidates()
    {
        const string Css =
            "@theme prefix(viu) { --color-brand: #123456; }\n" +
            "@utility brand-surface { background-color: var(--viu-color-brand); }";
        var options = new UtilityProjectStylesheetCompilationOptions
        {
            Theme = UtilityThemeParser.Parse(Css).Theme,
        };

        var result = UtilityProjectStylesheetCompiler.GetCompletions(
            Css,
            new UtilityClassCompletionQuery
            {
                Prefix = "viu",
                MaximumItems = 1_000,
            },
            options);

        result.Items.ShouldContain(
            item => item.CandidateText == "viu:brand-surface");
    }

    [Fact]
    public void GetCompletions_ProjectQuery_AppliesGlobalBudgetAndTruncation()
    {
        var result = UtilityProjectStylesheetCompiler.GetCompletions(
            "@utility first-project { display: block; }\n" +
            "@utility second-project { display: grid; }",
            new UtilityClassCompletionQuery
            {
                MaximumItems = 1,
            });

        result.Items.Count.ShouldBe(1);
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public void GetCompletions_IdenticalQuery_ProducesStructurallyEqualResult()
    {
        const string Css =
            "@utility brand-surface { background-color: rebeccapurple; }";
        var query = new UtilityClassCompletionQuery
        {
            Prefix = "brand-",
            MaximumItems = 20,
        };

        var first = UtilityProjectStylesheetCompiler.GetCompletions(Css, query);
        var second = UtilityProjectStylesheetCompiler.GetCompletions(Css, query);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }
}
