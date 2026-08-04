using System;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityCssRegistryTests
{
    [Fact]
    public void CompletionItems_EveryItemResolvesToIdenticalCompilerAndEditorMetadata()
    {
        var registry = UtilityCssRegistry.BuiltIn;

        registry.CompletionItems.Count.ShouldBeGreaterThan(30);
        foreach (var completion in registry.CompletionItems)
        {
            var hover = registry.Resolve(completion.CandidateText);
            var compilation = UtilityCssCompiler.Compile(
                new[] { completion.CandidateText });

            hover.IsSuccess.ShouldBeTrue(completion.CandidateText);
            hover.Metadata.ShouldBe(completion);
            compilation.Diagnostics.ShouldBeEmpty();
            compilation.Rules.ShouldHaveSingleItem().ShouldBe(completion);
            compilation.Css.ShouldContain(
                completion.Css.Split('\n')[0]);
        }
    }

    [Fact]
    public void GetCompletions_PrefixFilterPreservesRegistryOrder()
    {
        var registry = UtilityCssRegistry.BuiltIn;

        var completions = registry.GetCompletions("bg-");

        completions.ShouldNotBeEmpty();
        completions.ShouldAllBe(
            item => item.CandidateText.StartsWith(
                "bg-",
                StringComparison.Ordinal));
        completions.Select(item => item.SortOrder)
            .ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void DefaultTheme_LookupsAndCollectionsExposeSameImmutableValues()
    {
        var theme = UtilityTheme.Default;

        theme.TryGetSpacing("4", out var spacing).ShouldBeTrue();
        spacing.ShouldBe("calc(var(--spacing) * 4)");
        theme.Spacing.Single(token => token.Name == "4").Value.ShouldBe(spacing);

        theme.TryGetColor("blue-500", out var color).ShouldBeTrue();
        color.ShouldBe("var(--color-blue-500)");
        theme.Colors.Single(token => token.Name == "blue-500").Value.ShouldBe(color);

        theme.TryGetBreakpoint("2xl", out var breakpoint).ShouldBeTrue();
        breakpoint.ShouldBe("96rem");
    }

    [Fact]
    public void Resolve_UnsupportedUtility_ReturnsRecoverableDiagnostic()
    {
        var result = UtilityCssRegistry.BuiltIn.Resolve("prose");

        result.IsSuccess.ShouldBeFalse();
        result.Metadata.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(
            UtilityCssDiagnosticCode.UnsupportedUtility);
    }
}
