using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilityStylesheetParserTests
{
    [Fact]
    public void Parse_FunctionalUtility_ModelsResolutionModesAndDefaults()
    {
        // Compatibility references:
        // https://tailwindcss.com/docs/adding-custom-styles#functional-utilities
        // https://tailwindcss.com/blog/tailwindcss-v4-3#default-values-for-functional-utilities
        var result = UtilityStylesheetParser.Parse(
            """
            @utility text-* {
              font-size: --value(--text-*, [length], "inherit", --default(1rem));
              line-height: --modifier(--leading-*, [length], [*], --default(1));
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Directives.ShouldHaveSingleItem();
        result.Directives[0].Identifier.ShouldBe("text-*");
        result.Directives[0].IsValid.ShouldBeTrue();
        result.Functions.Select(function => function.Kind)
            .ShouldBe(
                new[]
                {
                    UtilityCssFunctionKind.Value,
                    UtilityCssFunctionKind.Default,
                    UtilityCssFunctionKind.Modifier,
                    UtilityCssFunctionKind.Default,
                });
        result.Functions[0].Arguments.Select(argument => argument.Kind)
            .ShouldBe(
                new[]
                {
                    UtilityCssFunctionArgumentKind.Theme,
                    UtilityCssFunctionArgumentKind.Arbitrary,
                    UtilityCssFunctionArgumentKind.Literal,
                    UtilityCssFunctionArgumentKind.Default,
                });
        result.Functions[2].Arguments.Select(argument => argument.Kind)
            .ShouldBe(
                new[]
                {
                    UtilityCssFunctionArgumentKind.Theme,
                    UtilityCssFunctionArgumentKind.Arbitrary,
                    UtilityCssFunctionArgumentKind.Arbitrary,
                    UtilityCssFunctionArgumentKind.Default,
                });
        result.Css.ShouldBe(
            """
            @utility text-* {
              font-size: --value(--text-*, [length], "inherit", --default(1rem));
              line-height: --modifier(--leading-*, [length], [*], --default(1));
            }
            """);
    }

    [Fact]
    public void Parse_ComplexUtility_BalancesNestedCssAndIgnoresQuotedOrCommentedSyntax()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            @utility scrollbar-hidden {
              &::-webkit-scrollbar {
                content: "} ; @apply not-a-directive";
                display: none;
              }
              /* @utility ignored-* { width: --value(integer); } */
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Directives.ShouldHaveSingleItem();
        var body = result.Directives[0].Body;
        body.ShouldNotBeNull();
        body.ShouldContain("&::-webkit-scrollbar");
        body.ShouldContain("@apply not-a-directive");
        result.Functions.ShouldBeEmpty();
        result.Css.ShouldContain("content: \"} ; @apply not-a-directive\";");
    }

    [Fact]
    public void Parse_CustomVariants_SupportsShorthandAndSlotBlockForms()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/adding-custom-styles#adding-custom-variants
        var result = UtilityStylesheetParser.Parse(
            """
            @custom-variant theme-midnight (&:where([data-theme="midnight"] *));
            @custom-variant pointer-fine {
              @media (pointer: fine) {
                @slot;
              }
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Directives.Count.ShouldBe(2);
        result.Directives[0].Kind.ShouldBe(UtilityDirectiveKind.CustomVariant);
        result.Directives[0].Identifier.ShouldBe("theme-midnight");
        result.Directives[0].HasBlock.ShouldBeFalse();
        result.Directives[1].Identifier.ShouldBe("pointer-fine");
        result.Directives[1].HasBlock.ShouldBeTrue();
        var blockBody = result.Directives[1].Body;
        blockBody.ShouldNotBeNull();
        blockBody.ShouldContain("@slot;");
    }

    [Fact]
    public void Parse_CompositionDirectives_PreservesNestingAndBalancedVariantParameters()
    {
        // Compatibility reference:
        // https://tailwindcss.com/docs/functions-and-directives
        var result = UtilityStylesheetParser.Parse(
            """
            @reference "./application.css";
            .card {
              @apply rounded-lg content-['a;b'] bg-[url("x;y")] hover:bg-brand;
              @variant dark:hover, focus {
                color: --alpha(var(--color-brand) / 50%);
              }
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Directives.Select(directive => directive.Kind)
            .ShouldBe(
                new[]
                {
                    UtilityDirectiveKind.Reference,
                    UtilityDirectiveKind.Apply,
                    UtilityDirectiveKind.Variant,
                });
        result.Directives.Select(directive => directive.NestingDepth)
            .ShouldBe(
                new[]
                {
                    0,
                    1,
                    1,
                });
        result.Directives[0].Identifier.ShouldBe("./application.css");
        result.Directives[1].Parameters.ShouldBe(
            "rounded-lg content-['a;b'] bg-[url(\"x;y\")] hover:bg-brand");
        result.Directives[2].Parameters.ShouldBe("dark:hover, focus");
        result.Functions.ShouldHaveSingleItem();
        result.Functions[0].Kind.ShouldBe(UtilityCssFunctionKind.Alpha);
        result.Functions[0].Arguments.ShouldHaveSingleItem();
        result.Functions[0].Arguments[0].Kind.ShouldBe(
            UtilityCssFunctionArgumentKind.Expression);
    }

    [Fact]
    public void Parse_SpacingAndAlphaFunctions_AcceptsNestedAndQuotedExpressions()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            .example {
              gap: calc(--spacing(4) - --spacing(.5));
              color: --alpha(color(display-p3 1 0 0) / calc(1 / 2));
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Functions.Select(function => function.Kind)
            .ShouldBe(
                new[]
                {
                    UtilityCssFunctionKind.Spacing,
                    UtilityCssFunctionKind.Spacing,
                    UtilityCssFunctionKind.Alpha,
                });
        result.Functions[0].Arguments[0].Text.ShouldBe("4");
        result.Functions[1].Arguments[0].Text.ShouldBe(".5");
        result.Functions[2].Arguments[0].Text.ShouldBe(
            "color(display-p3 1 0 0) / calc(1 / 2)");
    }

    [Fact]
    public void Parse_SourceContext_MapsEveryNodeAndArgumentToAbsoluteOffsets()
    {
        const int contentOffset = 71;
        const string sourceIdentity = "components/card.vue";
        const string css =
            "@utility tab-* { tab-size: --value(integer, --default(4)); }";
        var result = UtilityStylesheetParser.Parse(
            css,
            new UtilityStylesheetParseOptions
            {
                ContentOffset = contentOffset,
                SourceIdentity = sourceIdentity,
            });

        result.Diagnostics.ShouldBeEmpty();
        result.Directives[0].SourceSpan.SourceIdentity.ShouldBe(sourceIdentity);
        result.Directives[0].SourceSpan.Start.ShouldBe(contentOffset);
        result.Directives[0].ParametersSourceSpan.Start.ShouldBe(
            contentOffset + css.IndexOf("tab-*", StringComparison.Ordinal));
        result.Functions[0].SourceSpan.Start.ShouldBe(
            contentOffset + css.IndexOf("--value", StringComparison.Ordinal));
        var integer = result.Functions[0].Arguments[0];
        integer.SourceSpan.SourceIdentity.ShouldBe(sourceIdentity);
        css.Substring(
                integer.SourceSpan.Start - contentOffset,
                integer.SourceSpan.Length)
            .ShouldBe("integer");
    }

    [Fact]
    public void Parse_MultipleDefinitions_PreservesSourceOrderAndStructuralEquality()
    {
        const string css =
            """
            @utility tab-* { tab-size: --value(integer); }
            @utility tab-* { tab-size: --value(--tab-size-*); }
            """;

        var first = UtilityStylesheetParser.Parse(css);
        var repeated = UtilityStylesheetParser.Parse(css);

        first.Diagnostics.ShouldBeEmpty();
        first.Directives.Count.ShouldBe(2);
        first.Directives[0].Identifier.ShouldBe("tab-*");
        first.Directives[1].Identifier.ShouldBe("tab-*");
        first.ShouldBe(repeated);
        first.GetHashCode().ShouldBe(repeated.GetHashCode());
        first.Css.ShouldBe(repeated.Css);
    }

    [Fact]
    public void Parse_MalformedDirectives_ReportsErrorsAndRecoversFollowingDefinitions()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            @utility Invalid;
            @reference application.css;
            @custom-variant missing-slot {
              &:hover { color: red; }
            }
            @utility tab-* {
              tab-size: 4;
            }
            @utility safe {
              display: block;
            }
            """);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.MissingBlock);
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.InvalidUtilityName);
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.InvalidReference);
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.MissingVariantSlot);
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.MissingFunctionalValue);
        result.Directives.Last().Identifier.ShouldBe("safe");
        result.Directives.Last().IsValid.ShouldBeTrue();
        result.Css.ShouldContain("@utility safe");
        result.Css.ShouldNotContain("@utility Invalid");
        result.Css.ShouldNotContain("@utility tab-*");
    }

    [Fact]
    public void Parse_UnsupportedExecutableAndFrameworkDependencies_AreExplicitlyRejected()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            @plugin "@tailwindcss/forms";
            @config "./tailwind.config.js";
            @reference "tailwindcss";
            @import "tailwindcss";
            @utility safe {
              display: block;
            }
            """);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Count(
                diagnostic =>
                    diagnostic.Code ==
                    UtilityDirectiveDiagnosticCode.UnsupportedExecutableDirective)
            .ShouldBe(2);
        result.Diagnostics.Count(
                diagnostic =>
                    diagnostic.Code ==
                    UtilityDirectiveDiagnosticCode.UnsupportedFrameworkDependency)
            .ShouldBe(2);
        result.Directives.Count.ShouldBe(2);
        result.Directives[0].Kind.ShouldBe(UtilityDirectiveKind.Reference);
        result.Directives[0].IsValid.ShouldBeFalse();
        result.Directives[1].Identifier.ShouldBe("safe");
        result.Css.ShouldBe(
            """
            @utility safe {
              display: block;
            }
            """);
    }

    [Fact]
    public void Parse_InvalidFunctionArgumentsAndPlacement_AreSourceLocatedAndRecoverable()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            .example {
              width: --value(integer);
              color: --alpha(red);
              margin: --spacing();
              padding: --default(1rem);
            }
            @utility tab-* {
              tab-size: --value(length, [unsupported]);
              line-height: --modifier();
            }
            """);

        result.IsSuccess.ShouldBeFalse();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.InvalidFunctionPlacement);
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.InvalidFunctionArguments);
        result.Functions.Count.ShouldBe(6);
        result.Functions.Count(function => function.IsValid)
            .ShouldBe(0);
        result.Directives.ShouldHaveSingleItem();
        result.Directives[0].IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Parse_NestedRootOnlyDirectives_ReportsPlacementWithoutDroppingOuterCss()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            .container {
              @utility nested {
                display: block;
              }
              @reference "./application.css";
            }
            """);

        result.Diagnostics.Count.ShouldBe(2);
        result.Diagnostics.ShouldAllBe(
            diagnostic =>
                diagnostic.Code ==
                UtilityDirectiveDiagnosticCode.InvalidPlacement);
        result.Directives.ShouldAllBe(directive => !directive.IsValid);
        result.Css.ShouldBeEmpty();
    }

    [Fact]
    public void Emit_EquivalentLayout_ProducesCanonicalLineEndingsAndIndentation()
    {
        var first = UtilityStylesheetParser.Parse(
            "@utility content-auto {\r\n\tcontent-visibility: auto;\r\n}");
        var second = UtilityStylesheetParser.Parse(
            "@utility content-auto { content-visibility: auto; }");

        first.Diagnostics.ShouldBeEmpty();
        second.Diagnostics.ShouldBeEmpty();
        first.Css.ShouldBe(second.Css);
        first.Css.ShouldBe(
            """
            @utility content-auto {
              content-visibility: auto;
            }
            """);
    }

    [Fact]
    public void Parse_UnterminatedBlockAndFunction_KeepPartialModels()
    {
        var result = UtilityStylesheetParser.Parse(
            "@utility tab-* { tab-size: --value(integer");

        result.IsSuccess.ShouldBeFalse();
        result.Directives.ShouldHaveSingleItem();
        result.Functions.ShouldHaveSingleItem();
        var body = result.Directives[0].Body;
        body.ShouldNotBeNull();
        body.ShouldContain("--value(integer");
        result.Directives[0].IsValid.ShouldBeFalse();
        result.Functions[0].IsValid.ShouldBeFalse();
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.UnterminatedBlock);
        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldContain(UtilityDirectiveDiagnosticCode.UnterminatedFunction);
    }

    [Fact]
    public void Parse_UnbalancedDelimiter_RecoversAtFollowingStatement()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            .example {
              @apply bg-[red);
            }
            @utility safe {
              display: block;
            }
            """);

        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Code.ShouldBe(
            UtilityDirectiveDiagnosticCode.UnbalancedDelimiter);
        result.Directives.Count.ShouldBe(2);
        result.Directives[0].IsValid.ShouldBeFalse();
        result.Directives[1].Identifier.ShouldBe("safe");
        result.Directives[1].IsValid.ShouldBeTrue();
        result.Css.ShouldBe(
            """
            @utility safe {
              display: block;
            }
            """);
    }

    [Fact]
    public void Parse_NegativeFunctionalUtility_AcceptsV4ExplicitDefinition()
    {
        var result = UtilityStylesheetParser.Parse(
            """
            @utility -inset-* {
              inset: --spacing(--value(integer) * -1);
              inset: calc(--value([percentage], [length]) * -1);
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Directives[0].Identifier.ShouldBe("-inset-*");
        result.Functions.Select(function => function.Kind)
            .ShouldBe(
                new[]
                {
                    UtilityCssFunctionKind.Spacing,
                    UtilityCssFunctionKind.Value,
                    UtilityCssFunctionKind.Value,
                });
    }

    [Fact]
    public void Parse_V433UtilityNames_AcceptsStaticSpecialCharactersAndDoubleDashFunctions()
    {
        // Tagged compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var result = UtilityStylesheetParser.Parse(
            """
            @utility push-1\/2 { right: 50%; }
            @utility push-50% { right: 50%; }
            @utility density-1.5 { opacity: .5; }
            @utility foo_Bar { display: block; }
            @utility border--* { border-color: --value(--color-border-*); }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Directives.Select(directive => directive.Identifier)
            .ShouldBe(
                new[]
                {
                    "push-1/2",
                    "push-50%",
                    "density-1.5",
                    "foo_Bar",
                    "border--*",
                });
    }

    [Fact]
    public void Parse_V433ThemeFunctionKeys_AcceptsImplicitWildcardEscapesAndSubNamespaces()
    {
        // Tagged compatibility vectors:
        // https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/utilities.test.ts
        var result = UtilityStylesheetParser.Parse(
            """
            @utility example-* {
              width: --value(--example);
              height: --value(--example-\*);
              line-height: --value(--text-* --line-height);
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.Functions.Count.ShouldBe(3);
        result.Functions.ShouldAllBe(function => function.IsValid);
        result.Functions.ShouldAllBe(
            function =>
                function.Arguments.ShouldHaveSingleItem().Kind ==
                UtilityCssFunctionArgumentKind.Theme);
    }

    [Fact]
    public void Parse_NegativeContentOffset_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => UtilityStylesheetParser.Parse(
                "@utility safe { display: block; }",
                new UtilityStylesheetParseOptions
                {
                    ContentOffset = -1,
                }));
    }

    [Fact]
    public void Parse_CanceledInput_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityStylesheetParser.Parse(
                "@utility safe { display: block; }",
                null,
                cancellationSource.Token));
    }
}
