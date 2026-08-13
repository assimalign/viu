using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilityProjectStylesheetCompilerTests
{
    [Fact]
    public void Compile_FunctionalUtilities_ResolveThemeBareLiteralArbitraryAndDefaultValues()
    {
        // Compatibility references:
        // https://tailwindcss.com/docs/adding-custom-styles#functional-utilities
        // https://tailwindcss.com/blog/tailwindcss-v4-3#default-values-for-functional-utilities
        var css =
            """
            @theme {
              --tab-size-github: 8;
            }
            @utility tab-* {
              tab-size: --value(--tab-size-*, integer, [integer], "inherit", --default(4));
            }
            """;
        var theme = UtilityThemeParser.Parse(css).Theme;

        var result = Compile(
            css,
            theme,
            "tab-github",
            "tab-76",
            "tab-[7]",
            "tab-inherit",
            "tab");

        result.IsSuccess.ShouldBeTrue();
        result.Rules.Count.ShouldBe(5);
        result.UtilityCss.ShouldContain(".tab-github");
        result.UtilityCss.ShouldContain("tab-size: var(--tab-size-github);");
        result.UtilityCss.ShouldContain("tab-size: 76;");
        result.UtilityCss.ShouldContain("tab-size: 7;");
        result.UtilityCss.ShouldContain("tab-size: inherit;");
        result.UtilityCss.ShouldContain(".tab {");
        result.UtilityCss.ShouldContain("tab-size: 4;");
        result.AuthoredCss.ShouldNotContain("@theme");
        result.AuthoredCss.ShouldNotContain("@utility");
    }

    [Fact]
    public void Compile_ModifiersFractionsNegativeSpacingAndAlpha_ResolveExecutableCss()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/adding-custom-styles#modifiers
        var css =
            """
            @theme {
              --leading-6: 1.5rem;
            }
            @utility text-* {
              font-size: --value(--text-*, [length]);
              line-height: --modifier(--leading-*, [length], [*], --default(1));
            }
            @utility aspect-* {
              aspect-ratio: --value(--aspect-ratio-*, ratio, [ratio]);
            }
            @utility -inset-* {
              inset: --spacing(--value(integer) * -1);
            }
            .palette {
              padding: --spacing(4);
              color: --alpha(var(--color-blue-500) / 50%);
            }
            """;

        var result = Compile(
            css,
            UtilityThemeParser.Parse(css).Theme,
            "text-lg/6",
            "text-[13px]/[1.4]",
            "aspect-3/4",
            "-inset-4");

        result.IsSuccess.ShouldBeTrue();
        result.UtilityCss.ShouldContain("font-size: var(--text-lg);");
        result.UtilityCss.ShouldContain("line-height: var(--leading-6);");
        result.UtilityCss.ShouldContain("font-size: 13px;");
        result.UtilityCss.ShouldContain("line-height: 1.4;");
        result.UtilityCss.ShouldContain("aspect-ratio: 3 / 4;");
        result.UtilityCss.ShouldContain(
            "inset: calc(var(--spacing) * 4 * -1);");
        result.AuthoredCss.ShouldContain(
            "padding: calc(var(--spacing) * 4);");
        result.AuthoredCss.ShouldContain(
            "color: color-mix(in oklab, var(--color-blue-500) 50%, transparent);");
    }

    [Fact]
    public void Compile_InlineSpacingAndNumericAlpha_FoldLikeV433CssFunctions()
    {
        // Tagged compatibility references:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/css-functions.ts
        var css =
            """
            @theme inline reference {
              --spacing: 4px;
            }
            @utility offset-* {
              margin: --spacing(--value(number));
            }
            .palette {
              color: --alpha(red / 0.5);
              border-color: --alpha(red / 1);
            }
            """;

        var result = Compile(
            css,
            UtilityThemeParser.Parse(css).Theme,
            "offset-12");

        result.IsSuccess.ShouldBeTrue();
        result.UtilityCss.ShouldContain("margin: 48px;");
        result.AuthoredCss.ShouldContain(
            "color: color-mix(in oklab, red 50%, transparent);");
        result.AuthoredCss.ShouldContain("border-color: red;");
    }

    [Fact]
    public void Compile_UnmatchedFunctionalDeclarations_AreOmittedWhenAnotherModeResolves()
    {
        // Tailwind v4 omits declarations whose --value() mode does not match:
        // https://tailwindcss.com/docs/adding-custom-styles#supporting-theme-bare-and-arbitrary-values-together
        var css =
            """
            @utility opacity-* {
              opacity: --value([percentage]);
              opacity: calc(--value(integer) * 1%);
              opacity: --value(--opacity-*);
            }
            """;

        var result = UtilityProjectStylesheetCompiler.Compile(
            css,
            new[] { "opacity-75" });

        result.IsSuccess.ShouldBeTrue();
        var rule = result.Rules.ShouldHaveSingleItem();
        rule.Css.ShouldContain("opacity: calc(75 * 1%);");
        rule.Css.ShouldNotContain("VIU_UNRESOLVED");
        rule.Css.Count(character => character == ';').ShouldBe(1);
    }

    [Fact]
    public void Compile_ArbitraryTypeHintsAndUrls_SelectMatchingDeclarationsAndPreserveUnderscores()
    {
        var css =
            """
            @utility paint-* {
              width: --value([length]);
              color: --value([color]);
              background-image: --value([url]);
            }
            """;

        var result = UtilityProjectStylesheetCompiler.Compile(
            css,
            new[]
            {
                "paint-[color:var(--brand)]",
                "paint-[url(https://example.test/what_a_rush.png)]",
            });

        result.IsSuccess.ShouldBeTrue();
        result.Rules.Count.ShouldBe(2);
        var color = result.Rules.Single(
            rule => rule.CandidateText.Contains(
                "color:",
                StringComparison.Ordinal));
        color.Css.ShouldContain("color: var(--brand);");
        color.Css.ShouldNotContain("width:");
        color.Css.ShouldNotContain("background-image:");
        var image = result.Rules.Single(
            rule => rule.CandidateText.Contains(
                "url(",
                StringComparison.Ordinal));
        image.Css.ShouldContain(
            "background-image: url(https://example.test/what_a_rush.png);");
        image.Css.ShouldNotContain("color:");
    }

    [Fact]
    public void Compile_RatioAndResolvedModifier_InvalidatesCandidate()
    {
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @utility proportion-* {
              aspect-ratio: --value(ratio);
              line-height: --modifier(integer, --default(1));
            }
            """,
            new[] { "proportion-3/4" });

        result.IsSuccess.ShouldBeFalse();
        result.Rules.ShouldBeEmpty();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityProjectStylesheetDiagnosticCode.UnresolvedFunctionalValue);
    }

    [Fact]
    public void Compile_RatioValue_OmitsNonRatioDeclarations()
    {
        // Tagged behavior:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @utility proportion-* {
              width: --value(number);
              aspect-ratio: --value(ratio);
            }
            """,
            new[] { "proportion-3/4" });

        result.IsSuccess.ShouldBeTrue();
        var rule = result.Rules.ShouldHaveSingleItem();
        rule.Css.ShouldContain("aspect-ratio: 3 / 4;");
        rule.Css.ShouldNotContain("width:");
    }

    [Fact]
    public void Compile_BareFunctionalTypes_UseV433ConstraintsAndRatioFormatting()
    {
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @utility example-* {
              --value-as-number: --value(number);
              --value-as-percentage: --value(percentage);
              --value-as-ratio: --value(ratio);
            }
            """,
            new[]
            {
                "example-0.5",
                "example-1.23",
                "example-20%",
                "example-12.34%",
                "example-2/3",
                "example-1.2/3",
            });

        result.IsSuccess.ShouldBeFalse();
        result.Rules.Count.ShouldBe(3);
        result.UtilityCss.ShouldContain("--value-as-number: 0.5;");
        result.UtilityCss.ShouldContain("--value-as-percentage: 20%;");
        result.UtilityCss.ShouldContain("--value-as-ratio: 2 / 3;");
        result.UtilityCss.ShouldNotContain("1.23");
        result.UtilityCss.ShouldNotContain("12.34%");
    }

    [Fact]
    public void Compile_ExplicitArbitraryTypeHints_AcceptCssVariableShorthand()
    {
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @utility example-* {
              z-index: --value([integer]);
            }
            """,
            new[]
            {
                "example-[integer:var(--my-value)]",
                "example-(integer:--my-value)",
                "example-(integer:my-value)",
            });

        result.IsSuccess.ShouldBeTrue();
        result.Rules.Count.ShouldBe(3);
        result.Rules.ShouldAllBe(
            rule => rule.Css.Contains(
                "z-index: var(--my-value);",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_GlobalImportant_AppliesToCustomRulesAndCustomApply()
    {
        var importantTheme = UtilityThemeParser.Parse(
                string.Empty,
                new UtilityThemeParseOptions
                {
                    IsImportant = true,
                })
            .Theme;
        var result = Compile(
            """
            @utility notice {
              color: red;
            }
            .panel {
              @apply notice;
            }
            """,
            importantTheme,
            "notice");

        result.IsSuccess.ShouldBeTrue();
        result.UtilityCss.ShouldContain("color: red !important;");
        result.AuthoredCss.ShouldContain("color: red !important;");
    }

    [Fact]
    public void Compile_V433StaticNamesDoubleDashAndThemeSubNamespaces_ResolveTaggedVectors()
    {
        // Tagged compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var css =
            """
            @theme reference {
              --color-border-0: #e5e7eb;
              --text-xs: 0.75rem;
              --text-xs--line-height: calc(1 / 0.75);
            }
            @utility push-1\/2 { right: 50%; }
            @utility push-50% { right: 50%; }
            @utility border--* {
              border-color: --value(--color-border);
            }
            @utility typography-* {
              font-size: --value(--text);
              line-height: --value(--text-* --line-height);
            }
            """;
        var theme = UtilityThemeParser.Parse(css).Theme;

        var result = Compile(
            css,
            theme,
            "push-1/2",
            "push-50%",
            "border--0",
            "typography-xs");

        result.IsSuccess.ShouldBeTrue();
        result.Rules.Count.ShouldBe(4);
        result.UtilityCss.ShouldContain("right: 50%;");
        result.UtilityCss.ShouldContain(
            "border-color: var(--color-border-0, #e5e7eb);");
        result.UtilityCss.ShouldContain("font-size: var(--text-xs, 0.75rem);");
        result.UtilityCss.ShouldContain(
            "line-height: var(--text-xs--line-height, calc(1 / 0.75));");
    }

    [Fact]
    public void Compile_ApplyBuiltInAndCustomUtilities_RewritesAuthoredDeclarations()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/functions-and-directives#apply-directive
        var css =
            """
            @utility content-auto {
              content-visibility: auto;
            }
            .card {
              @apply p-4 content-auto;
            }
            """;

        var result = UtilityProjectStylesheetCompiler.Compile(
            css,
            Array.Empty<string>());

        result.IsSuccess.ShouldBeTrue();
        result.AuthoredCss.ShouldNotContain("@apply");
        result.AuthoredCss.ShouldNotContain("@utility");
        result.AuthoredCss.ShouldContain(
            "padding: calc(var(--spacing) * 4);");
        result.AuthoredCss.ShouldContain("content-visibility: auto;");
    }

    [Fact]
    public void Compile_ConfigurationDirectives_AreRemovedFromBrowserReadyAuthoredCss()
    {
        var css =
            """
            @import "viu-utilities" source(none) theme(static);
            @import "./ordinary.css";
            @source "../components";
            @source inline("hover:{flex,grid}");
            @theme {
              --color-brand: rebeccapurple;
            }
            .card {
              color: var(--color-brand);
            }
            """;
        var result = Compile(
            css,
            UtilityThemeParser.Parse(css).Theme);

        result.IsSuccess.ShouldBeTrue();
        result.AuthoredCss.ShouldNotContain("viu-utilities");
        result.AuthoredCss.ShouldNotContain("@source");
        result.AuthoredCss.ShouldNotContain("@theme");
        result.AuthoredCss.ShouldContain("@import \"./ordinary.css\";");
        result.AuthoredCss.ShouldContain(".card");
        result.AuthoredCss.ShouldContain("color: var(--color-brand);");
    }

    [Fact]
    public void Compile_CustomVariantsAndAuthoredVariant_ExpandSelectorAndSlotForms()
    {
        // Compatibility references:
        // https://tailwindcss.com/docs/adding-custom-styles#adding-custom-variants
        // https://tailwindcss.com/docs/adding-custom-styles#using-variants
        var css =
            """
            @custom-variant theme-midnight (&:where([data-theme="midnight"] *));
            @custom-variant any-hover {
              @media (any-hover: hover) {
                &:hover {
                  @slot;
                }
              }
            }
            @utility content-auto {
              content-visibility: auto;
            }
            .panel {
              @variant theme-midnight {
                color: white;
              }
              @variant any-hover {
                color: blue;
              }
            }
            """;

        var result = UtilityProjectStylesheetCompiler.Compile(
            css,
            new[]
            {
                "theme-midnight:content-auto",
                "any-hover:content-auto",
            });

        result.IsSuccess.ShouldBeTrue();
        result.Rules.Count.ShouldBe(2);
        result.UtilityCss.ShouldContain(
            "&:where([data-theme=\"midnight\"] *)");
        result.UtilityCss.ShouldContain("@media (any-hover: hover)");
        result.UtilityCss.ShouldContain("&:hover");
        result.AuthoredCss.ShouldNotContain("@variant");
        result.AuthoredCss.ShouldContain("color: white;");
        result.AuthoredCss.ShouldContain("color: blue;");
    }

    [Fact]
    public void Compile_CustomVariant_AppliesToBuiltInUtilityCandidates()
    {
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @custom-variant theme-midnight (&:where([data-theme="midnight"] *));
            """,
            new[]
            {
                "theme-midnight:bg-red-500",
                "theme-midnight:hover:bg-blue-500",
            });

        result.IsSuccess.ShouldBeTrue();
        result.Rules.Count.ShouldBe(2);
        result.UtilityCss.ShouldContain(
            ".theme-midnight\\:bg-red-500");
        result.UtilityCss.ShouldContain(
            "background-color: var(--color-red-500);");
        result.UtilityCss.ShouldContain(
            ".theme-midnight\\:hover\\:bg-blue-500");
        result.UtilityCss.ShouldContain(
            "background-color: var(--color-blue-500);");
        result.UtilityCss.ShouldContain(
            "&:where([data-theme=\"midnight\"] *)");
        result.UtilityCss.ShouldContain("@media (hover: hover)");
        result.UtilityCss.ShouldContain("&:hover");
    }

    [Fact]
    public void Compile_CustomVariant_ComposesBuiltInAndForwardCustomVariants()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/adding-custom-styles#adding-custom-variants
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @custom-variant hocus {
              @variant hover {
                @slot;
              }
              @variant focus {
                @slot;
              }
            }
            @custom-variant forwarded {
              @variant declared-later {
                @slot;
              }
            }
            @custom-variant declared-later (&[data-later]);
            """,
            new[]
            {
                "hocus:flex",
                "forwarded:grid",
            });

        result.IsSuccess.ShouldBeTrue();
        result.Rules.Count.ShouldBe(2);
        result.UtilityCss.ShouldNotContain("@variant");
        result.UtilityCss.ShouldNotContain("@slot");
        result.UtilityCss.ShouldContain("@media (hover: hover)");
        result.UtilityCss.ShouldContain("&:hover");
        result.UtilityCss.ShouldContain("&:focus");
        result.UtilityCss.ShouldContain("&[data-later]");
    }

    [Fact]
    public void Compile_CustomVariantCircularComposition_ReportsDiagnosticAndOmitsRule()
    {
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @custom-variant first {
              @variant second {
                @slot;
              }
            }
            @custom-variant second {
              @variant first {
                @slot;
              }
            }
            """,
            new[] { "first:flex" });

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityProjectStylesheetDiagnosticCode.CircularVariant);
        result.Rules.ShouldBeEmpty();
        result.UtilityCss.ShouldNotContain("@variant");
        result.UtilityCss.ShouldNotContain("@slot");
    }

    [Fact]
    public void Compile_BuiltInAuthoredVariant_UsesRegistryVariantTemplate()
    {
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            .panel {
              @variant hover, focus {
                color: blue;
              }
            }
            """,
            Array.Empty<string>());

        result.IsSuccess.ShouldBeTrue();
        result.AuthoredCss.ShouldNotContain("@variant");
        result.AuthoredCss.ShouldContain("@media (hover: hover)");
        result.AuthoredCss.ShouldContain("&:hover");
        result.AuthoredCss.ShouldContain("&:focus");
    }

    [Fact]
    public void Compile_PrefixedTheme_HandlesApplyAuthoredVariantAndMixedCustomCandidateVariants()
    {
        var css =
            """
            @theme prefix(viu) {
              --color-brand: rebeccapurple;
            }
            @custom-variant theme-midnight (&:where([data-theme="midnight"] *));
            @utility brand-surface {
              background-color: var(--viu-color-brand);
            }
            .surface {
              @apply viu:brand-surface;
              @variant hover {
                color: white;
              }
            }
            """;
        var theme = UtilityThemeParser.Parse(css).Theme;

        var result = Compile(
            css,
            theme,
            "viu:theme-midnight:hover:brand-surface");

        result.IsSuccess.ShouldBeTrue();
        result.AuthoredCss.ShouldContain(
            "background-color: var(--viu-color-brand);");
        result.AuthoredCss.ShouldContain("@media (hover: hover)");
        result.UtilityCss.ShouldContain(
            "&:where([data-theme=\"midnight\"] *)");
        result.UtilityCss.ShouldContain("&:hover");
    }

    [Fact]
    public void Compile_ReferenceGraph_ImportsDefinitionsWithoutDuplicatingReferencedCss()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/functions-and-directives#reference-directive
        var graph = new UtilityStylesheetReferenceGraph(
            new[]
            {
                new UtilityStylesheetReference(
                    "components/card.css",
                    "../application.css",
                    "application.css",
                    """
                    @utility brand-card {
                      border-color: rebeccapurple;
                    }
                    @custom-variant brand-state (&[data-brand]);
                    .must-not-be-copied {
                      color: red;
                    }
                    """),
            });
        var options = new UtilityProjectStylesheetCompilationOptions
        {
            SourceIdentity = "components/card.css",
            ReferenceGraph = graph,
        };

        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @reference "../application.css";
            .card {
              @apply brand-card;
            }
            """,
            new[] { "brand-state:brand-card" },
            options);

        result.IsSuccess.ShouldBeTrue();
        result.Utilities.ShouldHaveSingleItem().IsReferenced.ShouldBeTrue();
        result.Variants.ShouldHaveSingleItem().IsReferenced.ShouldBeTrue();
        result.AuthoredCss.ShouldContain("border-color: rebeccapurple;");
        result.AuthoredCss.ShouldNotContain(".must-not-be-copied");
        result.UtilityCss.ShouldContain("&[data-brand]");
    }

    [Fact]
    public void Compile_UnresolvedAndCyclicReferences_ReportExactSourceLocations()
    {
        const string rootIdentity = "root.css";
        const string rootCss =
            """
            @reference "./a.css";
            @reference "./missing.css";
            """;
        var graph = new UtilityStylesheetReferenceGraph(
            new[]
            {
                new UtilityStylesheetReference(
                    rootIdentity,
                    "./a.css",
                    "a.css",
                    "@reference \"./root.css\";"),
                new UtilityStylesheetReference(
                    "a.css",
                    "./root.css",
                    rootIdentity,
                    rootCss),
            });
        var result = UtilityProjectStylesheetCompiler.Compile(
            rootCss,
            Array.Empty<string>(),
            new UtilityProjectStylesheetCompilationOptions
            {
                SourceIdentity = rootIdentity,
                ReferenceGraph = graph,
            });

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldBe(
                new[]
                {
                    UtilityProjectStylesheetDiagnosticCode.CyclicReference,
                    UtilityProjectStylesheetDiagnosticCode.UnresolvedReference,
                });
        result.Diagnostics[0].SourceSpan.SourceIdentity.ShouldBe("a.css");
        result.Diagnostics[1].SourceSpan.SourceIdentity.ShouldBe(rootIdentity);
        result.Diagnostics[1].SourceSpan.Start.ShouldBe(
            rootCss.IndexOf(
                "\"./missing.css\"",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_VariantApplyExpandsWhileUnknownAndCircularApplyReportDiagnostics()
    {
        var result = UtilityProjectStylesheetCompiler.Compile(
            """
            @utility recursive {
              @apply recursive;
            }
            .card {
              @apply hover:p-4 missing;
            }
            """,
            new[] { "recursive" });

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityProjectStylesheetDiagnosticCode.CircularApply);
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityProjectStylesheetDiagnosticCode.UnknownAppliedUtility);
        result.AuthoredCss.ShouldContain("@media (hover: hover)");
        result.AuthoredCss.ShouldContain("&:hover");
        result.AuthoredCss.ShouldContain(
            "padding: calc(var(--spacing) * 4);");
        result.Rules.ShouldBeEmpty();
    }

    [Fact]
    public void Compile_ReorderedCandidatesAndReferenceEdges_ProducesIdenticalOutput()
    {
        const string css =
            """
            @reference "./shared.css";
            @utility local {
              display: grid;
            }
            """;
        var firstGraph = new UtilityStylesheetReferenceGraph(
            new[]
            {
                new UtilityStylesheetReference(
                    "root.css",
                    "./shared.css",
                    "shared.css",
                    "@utility shared { display: flex; }"),
            });
        var secondGraph = new UtilityStylesheetReferenceGraph(
            firstGraph.References.Reverse());
        var first = UtilityProjectStylesheetCompiler.Compile(
            css,
            new[] { "local", "shared" },
            CreateOptions(
                "root.css",
                UtilityTheme.Default,
                firstGraph));
        var second = UtilityProjectStylesheetCompiler.Compile(
            css,
            new[] { "shared", "local", "shared" },
            CreateOptions(
                "root.css",
                UtilityTheme.Default,
                secondGraph));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Compile_CanceledInput_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityProjectStylesheetCompiler.Compile(
                "@utility safe { display: block; }",
                new[] { "safe" },
                null,
                cancellationSource.Token));
    }

    private static UtilityProjectStylesheetCompilationResult Compile(
        string css,
        UtilityTheme theme,
        params string[] candidates) =>
        UtilityProjectStylesheetCompiler.Compile(
            css,
            candidates,
            CreateOptions(
                string.Empty,
                theme,
                UtilityStylesheetReferenceGraph.Empty));

    private static UtilityProjectStylesheetCompilationOptions CreateOptions(
        string sourceIdentity,
        UtilityTheme theme,
        UtilityStylesheetReferenceGraph referenceGraph) =>
        new()
        {
            SourceIdentity = sourceIdentity,
            Theme = theme,
            ReferenceGraph = referenceGraph,
        };
}
