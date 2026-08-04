using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.UtilityCss.Tests;

public sealed class UtilityThemeParserTests
{
    [Fact]
    public void Parse_CoreNamespaces_FeedCompilerResolution()
    {
        // Compatibility reference:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/theme.ts
        var result = UtilityThemeParser.Parse(
            """
            @theme {
              --spacing-18: 4.5rem;
              --color-brand: #123456;
              --breakpoint-tablet: 52rem;
              --text-display: 3rem;
              --font-weight-heavy: 850;
              --radius-card: 1.25rem;
            }
            """);

        result.Diagnostics.ShouldBeEmpty();

        var compilation = Compile(
            result.Theme,
            "p-18",
            "bg-brand",
            "tablet:bg-brand",
            "text-display",
            "font-heavy",
            "rounded-card");

        compilation.Diagnostics.ShouldBeEmpty();
        compilation.Css.ShouldContain("padding: var(--spacing-18);");
        compilation.Css.ShouldContain("background-color: var(--color-brand);");
        compilation.Css.ShouldContain("@media (width >= 52rem)");
        compilation.Css.ShouldContain("font-size: var(--text-display);");
        compilation.Css.ShouldContain("font-weight: var(--font-weight-heavy);");
        compilation.Css.ShouldContain("border-radius: var(--radius-card);");
    }

    [Fact]
    public void Parse_OptionsAndPrefix_ControlEmissionAndResolution()
    {
        var result = UtilityThemeParser.Parse(
            """
            @theme prefix(viu) {
              --color-normal: red;
              --brand-accent: chartreuse;
              --text-shadow-card: 0 1px 2px #0003;
              --text-display--line-height: 1;
            }
            @theme inline {
              --color-inline: var(--application-brand);
            }
            @theme static {
              --color-static: blue;
            }
            @theme reference {
              --color-reference: green;
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Theme.Prefix.ShouldBe("viu");
        result.Css.ShouldContain("--viu-color-normal: red;");
        result.Css.ShouldContain("--viu-color-inline: var(--application-brand);");
        result.Css.ShouldContain("--viu-color-static: blue;");
        result.Css.ShouldContain("--viu-brand-accent: chartreuse;");
        result.Css.ShouldNotContain("--viu-color-reference:");
        result.Theme.TryGetProperty("--brand-accent", out _).ShouldBeTrue();
        result.Theme.TryGetColor("brand-accent", out _).ShouldBeFalse();
        result.Theme.TryGetProperty("--text-shadow-card", out _).ShouldBeTrue();
        result.Theme.TryGetFontSize("shadow-card", out _).ShouldBeFalse();
        result.Theme.TryGetProperty(
            "--text-display--line-height",
            out _).ShouldBeTrue();
        result.Theme.TryGetFontSize(
            "display--line-height",
            out _).ShouldBeFalse();

        var inlineDeclaration = result.Declarations
            .Single(declaration => declaration.Name == "--color-inline");
        var staticDeclaration = result.Declarations
            .Single(declaration => declaration.Name == "--color-static");
        var referenceDeclaration = result.Declarations
            .Single(declaration => declaration.Name == "--color-reference");
        inlineDeclaration.Options.ShouldBe(UtilityThemeOptions.Inline);
        staticDeclaration.Options.ShouldBe(UtilityThemeOptions.Static);
        referenceDeclaration.Options.ShouldBe(UtilityThemeOptions.Reference);

        var compilation = Compile(
            result.Theme,
            "viu:bg-normal",
            "viu:bg-inline",
            "viu:bg-static",
            "viu:bg-reference");

        compilation.Diagnostics.ShouldBeEmpty();
        compilation.Css.ShouldContain("background-color: var(--viu-color-normal);");
        compilation.Css.ShouldContain("background-color: var(--application-brand);");
        compilation.Css.ShouldContain("background-color: var(--viu-color-static);");
        compilation.Css.ShouldContain(
            "background-color: var(--viu-color-reference, green);");

        var missingPrefix = Compile(result.Theme, "bg-normal");
        missingPrefix.Rules.ShouldBeEmpty();
        missingPrefix.Diagnostics.ShouldHaveSingleItem();
    }

    [Fact]
    public void Parse_ImportLevelPrefix_ConfiguresThemeBeforeAuthoredBlocks()
    {
        var result = UtilityThemeParser.Parse(
            """
            @theme {
              --color-brand: rebeccapurple;
            }
            """,
            new UtilityThemeParseOptions
            {
                Prefix = "vu",
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Theme.Prefix.ShouldBe("vu");
        result.Css.ShouldContain("--vu-color-brand: rebeccapurple;");
        Compile(result.Theme, "vu:bg-brand")
            .Css.ShouldContain("background-color: var(--vu-color-brand);");

        var conflict = UtilityThemeParser.Parse(
            "@theme prefix(other) { --color-brand: red; }",
            new UtilityThemeParseOptions
            {
                Prefix = "vu",
            });
        conflict.Diagnostics
            .ShouldHaveSingleItem()
            .Code.ShouldBe(UtilityThemeDiagnosticCode.ConflictingPrefix);
    }

    [Fact]
    public void Parse_ImportLevelThemeModes_ApplyOnlyToInheritedTheme()
    {
        var inline = UtilityThemeParser.Parse(
            string.Empty,
            new UtilityThemeParseOptions
            {
                ImportedThemeOptions = UtilityThemeOptions.Inline,
            });
        var inlineRule = Compile(inline.Theme, "bg-blue-500")
            .Rules
            .ShouldHaveSingleItem();
        inlineRule.Css.ShouldNotContain("var(--color-blue-500)");

        var authoredOverride = UtilityThemeParser.Parse(
            "@theme { --color-blue-500: rebeccapurple; }",
            new UtilityThemeParseOptions
            {
                ImportedThemeOptions = UtilityThemeOptions.Inline,
            });
        Compile(authoredOverride.Theme, "bg-blue-500")
            .Css.ShouldContain("background-color: var(--color-blue-500);");

        var staticTheme = UtilityThemeParser.Parse(
            string.Empty,
            new UtilityThemeParseOptions
            {
                ImportedThemeOptions = UtilityThemeOptions.Static,
            });
        staticTheme.Theme.Properties
            .ShouldAllBe(
                property =>
                    (property.Options & UtilityThemeOptions.Static) != 0);

        var importantTheme = UtilityThemeParser.Parse(
            string.Empty,
            new UtilityThemeParseOptions
            {
                IsImportant = true,
            });
        importantTheme.Theme.IsImportant.ShouldBeTrue();
        Compile(importantTheme.Theme, "p-4")
            .Css.ShouldContain(
                "padding: calc(var(--spacing) * 4) !important;");
    }

    [Fact]
    public void Parse_WildcardAndFullResets_RemoveInheritedThemeValues()
    {
        var namespaceReset = UtilityThemeParser.Parse(
            """
            @theme {
              --color-*: initial;
              --color-brand: red;
            }
            """);

        namespaceReset.Diagnostics.ShouldBeEmpty();
        namespaceReset.Theme.TryGetColor("blue-500", out _).ShouldBeFalse();
        namespaceReset.Theme.TryGetColor("brand", out _).ShouldBeTrue();
        namespaceReset.Declarations[0].IsReset.ShouldBeTrue();
        namespaceReset.Css.ShouldNotContain("--color-*");

        var compilation = Compile(
            namespaceReset.Theme,
            "bg-blue-500",
            "bg-brand");
        compilation.Rules.ShouldHaveSingleItem();
        compilation.Rules[0].CandidateText.ShouldBe("bg-brand");
        compilation.Diagnostics.ShouldHaveSingleItem();

        var fullReset = UtilityThemeParser.Parse(
            """
            @theme {
              --*: initial;
              --spacing-custom: 7px;
            }
            """);

        fullReset.Diagnostics.ShouldBeEmpty();
        fullReset.Theme.Properties.ShouldHaveSingleItem();
        fullReset.Theme.TryGetSpacing("custom", out var spacing).ShouldBeTrue();
        spacing.ShouldBe("var(--spacing-custom)");
        fullReset.Theme.TryGetFontSize("base", out _).ShouldBeFalse();
    }

    [Fact]
    public void Parse_DefaultDeclarations_YieldToAuthoredValuesAndOverrideDefaults()
    {
        var result = UtilityThemeParser.Parse(
            """
            @theme {
              --color-brand: red;
            }
            @theme default {
              --color-brand: blue;
              --color-blue-500: purple;
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Theme.TryGetProperty("--color-brand", out var brand).ShouldBeTrue();
        brand.ShouldNotBeNull();
        brand.Value.ShouldBe("red");
        result.Theme.TryGetProperty("--color-blue-500", out var blue).ShouldBeTrue();
        blue.ShouldNotBeNull();
        blue.Value.ShouldBe("purple");
        blue.Options.ShouldBe(UtilityThemeOptions.Default);
        result.Css.ShouldContain("--color-brand: red;");
        result.Css.ShouldNotContain("--color-brand: blue;");
        result.Css.ShouldContain("--color-blue-500: purple;");
    }

    [Fact]
    public void Parse_MalformedInput_ReportsExactSpansAndRecoversLaterDeclarations()
    {
        const int contentOffset = 37;
        const string sourceIdentity = "components/card.vue";
        const string css =
            """
            @theme prefix(Viu) {
              color-brand: red;
              --color-*: red;
              --color-broken red;
              --color-good: green;
            }
            @theme invalid;
            """;
        var result = UtilityThemeParser.Parse(
            css,
            new UtilityThemeParseOptions
            {
                ContentOffset = contentOffset,
                SourceIdentity = sourceIdentity,
            });

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldBe(
                new[]
                {
                    UtilityThemeDiagnosticCode.InvalidPrefix,
                    UtilityThemeDiagnosticCode.InvalidCustomProperty,
                    UtilityThemeDiagnosticCode.InvalidNamespaceReset,
                    UtilityThemeDiagnosticCode.InvalidDeclaration,
                    UtilityThemeDiagnosticCode.MissingBlock,
                });
        result.Diagnostics[0].SourceSpan.SourceIdentity.ShouldBe(sourceIdentity);
        result.Diagnostics[0].SourceSpan.Start.ShouldBe(
            contentOffset + css.IndexOf("prefix(Viu)", StringComparison.Ordinal));
        result.Diagnostics[0].SourceSpan.Length.ShouldBe("prefix(Viu)".Length);

        var good = result.Declarations
            .Single(declaration => declaration.Name == "--color-good");
        good.SourceSpan.SourceIdentity.ShouldBe(sourceIdentity);
        good.SourceSpan.Start.ShouldBe(
            contentOffset + css.IndexOf("--color-good", StringComparison.Ordinal));
        css.Substring(
                good.SourceSpan.Start - contentOffset,
                good.SourceSpan.Length)
            .ShouldBe("--color-good: green;");
        result.Theme.TryGetColor("good", out _).ShouldBeTrue();
    }

    [Fact]
    public void Parse_UnterminatedBlock_KeepsValidDeclarationsAndReportsError()
    {
        var result = UtilityThemeParser.Parse(
            "@theme { --spacing-18: 4.5rem;");

        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Code.ShouldBe(
            UtilityThemeDiagnosticCode.UnterminatedBlock);
        result.Theme.TryGetSpacing("18", out var spacing).ShouldBeTrue();
        spacing.ShouldBe("var(--spacing-18)");
    }

    [Fact]
    public void Parse_ConflictingPrefixes_ReportsDiagnosticAndKeepsFirstPrefix()
    {
        var result = UtilityThemeParser.Parse(
            """
            @theme prefix(viu) {
              --color-first: red;
            }
            @theme prefix(application) {
              --color-second: blue;
            }
            """);

        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Code.ShouldBe(
            UtilityThemeDiagnosticCode.ConflictingPrefix);
        result.Theme.Prefix.ShouldBe("viu");
        result.Css.ShouldContain("--viu-color-first: red;");
        result.Css.ShouldContain("--viu-color-second: blue;");
    }

    [Fact]
    public void Parse_EquivalentInputs_ProduceValueEquatableImmutableThemes()
    {
        const string firstCss =
            "@theme { --spacing-18: 4.5rem; --color-brand: #123456; }";
        const string secondCss =
            "@theme { --color-brand: #123456; --spacing-18: 4.5rem; }";

        var first = UtilityThemeParser.Parse(firstCss);
        var repeated = UtilityThemeParser.Parse(firstCss);
        var reordered = UtilityThemeParser.Parse(secondCss);

        first.ShouldBe(repeated);
        first.GetHashCode().ShouldBe(repeated.GetHashCode());
        first.Theme.ShouldBe(reordered.Theme);
        first.Theme.GetHashCode().ShouldBe(reordered.Theme.GetHashCode());
        first.Theme.Properties.ShouldBe(reordered.Theme.Properties);
    }

    [Fact]
    public void Parse_CanceledInput_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityThemeParser.Parse(
                "@theme { --color-brand: red; }",
                null,
                cancellationSource.Token));
    }

    private static UtilityCssCompilationResult Compile(
        UtilityTheme theme,
        params string[] candidates) =>
        UtilityCssCompiler.Compile(
            candidates,
            UtilityCssRegistry.BuiltIn,
            theme,
            CancellationToken.None);
}
