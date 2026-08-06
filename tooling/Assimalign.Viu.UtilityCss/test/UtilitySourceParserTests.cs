using System;
using System.Linq;
using System.Threading;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilitySourceParserTests
{
    [Fact]
    public void Parse_VirtualImportAndSourceForms_ProjectsImmutableConfiguration()
    {
        // Compatibility references:
        // https://tailwindcss.com/docs/detecting-classes-in-source-files
        // https://tailwindcss.com/docs/styling-with-utility-classes#using-the-important-flag
        const string css = """
            @import "viu-utilities" source("../Components") prefix(vu) theme(static) important;
            @source "../Shared";
            @source not "../Shared/Legacy";
            @source inline("vu:{hover:,focus:,}underline");
            @source not inline("vu:focus:underline");
            """;

        var result = UtilitySourceParser.Parse(css);

        result.IsSuccess.ShouldBeTrue();
        result.Configuration.HasUtilitiesImport.ShouldBeTrue();
        result.Configuration.IsAutomaticDetectionEnabled.ShouldBeTrue();
        result.Configuration.BasePath.ShouldBe("../Components");
        result.Configuration.Prefix.ShouldBe("vu");
        result.Configuration.IsImportant.ShouldBeTrue();
        result.Configuration.ImportedThemeOptions.ShouldBe(UtilityThemeOptions.Static);
        result.Configuration.IncludedPaths.ShouldBe(new[] { "../Shared" });
        result.Configuration.ExcludedPaths.ShouldBe(new[] { "../Shared/Legacy" });
        result.Configuration.IncludedCandidates.ShouldBe(
            new[]
            {
                "vu:hover:underline",
                "vu:focus:underline",
                "vu:underline",
            });
        result.Configuration.ExcludedCandidates.ShouldBe(
            new[] { "vu:focus:underline" });
    }

    [Fact]
    public void Parse_SourceNone_DisablesOnlyAutomaticDetection()
    {
        const string css = """
            @import "viu-utilities" source(none);
            @source "../Admin";
            @source inline("block");
            """;

        var result = UtilitySourceParser.Parse(css);

        result.IsSuccess.ShouldBeTrue();
        result.Configuration.IsAutomaticDetectionEnabled.ShouldBeFalse();
        result.Configuration.BasePath.ShouldBeNull();
        result.Configuration.IncludedPaths.ShouldBe(new[] { "../Admin" });
        result.Configuration.IncludedCandidates.ShouldBe(new[] { "block" });
    }

    [Fact]
    public void Parse_NestedBraceAndNumericRange_ExpandsInSourceOrder()
    {
        const string css =
            """@source inline("{hover:,}bg-red-{50,{100..300..100},950}");""";

        var result = UtilitySourceParser.Parse(css);

        result.IsSuccess.ShouldBeTrue();
        result.Configuration.IncludedCandidates.ShouldBe(
            new[]
            {
                "hover:bg-red-50",
                "hover:bg-red-100",
                "hover:bg-red-200",
                "hover:bg-red-300",
                "hover:bg-red-950",
                "bg-red-50",
                "bg-red-100",
                "bg-red-200",
                "bg-red-300",
                "bg-red-950",
            });
    }

    [Fact]
    public void Parse_DescendingAndPaddedRanges_PreservesRangeSemantics()
    {
        const string css = """
            @source inline("grid-cols-{03..01}");
            @source inline("-m-{4..0..2}");
            """;

        var result = UtilitySourceParser.Parse(css);

        result.IsSuccess.ShouldBeTrue();
        result.Configuration.IncludedCandidates.ShouldBe(
            new[]
            {
                "grid-cols-03",
                "grid-cols-02",
                "grid-cols-01",
                "-m-4",
                "-m-2",
                "-m-0",
            });
    }

    [Fact]
    public void Parse_CommentsStringsAndNestedBlocks_DoNotCreateFalseDirectives()
    {
        const string css = """
            /* @source inline("hidden"); */
            .example {
              content: "@source inline(\"hidden\")";
              @source inline("block");
            }
            @source inline("flex");
            """;

        var result = UtilitySourceParser.Parse(css);

        result.Configuration.IncludedCandidates.ShouldBe(new[] { "flex" });
        result.Diagnostics.ShouldHaveSingleItem()
            .Code.ShouldBe(UtilitySourceDiagnosticCode.InvalidPlacement);
    }

    [Fact]
    public void Parse_TailwindImport_IsRejectedWithoutTreatingOtherImportsAsConfiguration()
    {
        const string css = """
            @import url("https://example.test/fonts.css");
            @import "theme.css";
            @import "tailwindcss";
            """;

        var result = UtilitySourceParser.Parse(css);

        result.IsSuccess.ShouldBeFalse();
        result.Configuration.HasUtilitiesImport.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem()
            .Code.ShouldBe(UtilitySourceDiagnosticCode.TailwindDependencyNotAllowed);
    }

    [Fact]
    public void Parse_InlineCandidate_UsesHostSuppliedCustomVariantRegistry()
    {
        var definitions = UtilityVariantRegistry.BuiltIn.Definitions
            .Concat(
                new[]
                {
                    new UtilityVariantDefinition(
                        "night",
                        UtilityVariantKind.Static,
                        UtilityVariantCategory.State),
                });

        var result = UtilitySourceParser.Parse(
            """@source inline("vu:night:badge");""",
            new UtilitySourceParseOptions
            {
                Prefix = "vu",
                VariantRegistry = new UtilityVariantRegistry(definitions),
            });

        result.IsSuccess.ShouldBeTrue();
        result.Configuration.IncludedCandidates.ShouldBe(
            new[] { "vu:night:badge" });
    }

    [Theory]
    [InlineData("""@import "viu-utilities" prefix(Viu);""", UtilitySourceDiagnosticCode.InvalidPrefix)]
    [InlineData("""@import "viu-utilities" source(auto);""", UtilitySourceDiagnosticCode.InvalidImportModifier)]
    [InlineData("""@import "viu-utilities" important important;""", UtilitySourceDiagnosticCode.InvalidImportModifier)]
    [InlineData("""@source inline("{1..x}");""", UtilitySourceDiagnosticCode.InvalidInlineExpression)]
    [InlineData("""@source application.cs;""", UtilitySourceDiagnosticCode.InvalidSourceDirective)]
    public void Parse_MalformedConfiguration_ReportsSourceLocatedDiagnostic(
        string css,
        UtilitySourceDiagnosticCode expectedCode)
    {
        const string sourceIdentity = "Styles/application.css";
        const int contentOffset = 71;

        var result = UtilitySourceParser.Parse(
            css,
            new UtilitySourceParseOptions
            {
                SourceIdentity = sourceIdentity,
                ContentOffset = contentOffset,
            });

        result.IsSuccess.ShouldBeFalse();
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(expectedCode);
        diagnostic.SourceSpan.SourceIdentity.ShouldBe(sourceIdentity);
        diagnostic.SourceSpan.Start.ShouldBe(contentOffset);
        diagnostic.SourceSpan.Length.ShouldBe(css.Length);
    }

    [Fact]
    public void Parse_ExpansionLimit_RejectsOnlyAffectedDirective()
    {
        const string css = """
            @source inline("grid-cols-{1..4}");
            @source inline("block");
            """;

        var result = UtilitySourceParser.Parse(
            css,
            new UtilitySourceParseOptions
            {
                MaximumExpansionCount = 3,
            });

        result.Configuration.IncludedCandidates.ShouldBe(new[] { "block" });
        result.Diagnostics.ShouldHaveSingleItem()
            .Code.ShouldBe(UtilitySourceDiagnosticCode.ExpansionLimitExceeded);
    }

    [Fact]
    public void Parse_DuplicateCandidatesAndPaths_DeduplicatesByFirstOccurrence()
    {
        const string css = """
            @source "../Components";
            @source "../Components";
            @source inline("block {block,flex}");
            """;

        var result = UtilitySourceParser.Parse(css);

        result.Configuration.IncludedPaths.ShouldBe(new[] { "../Components" });
        result.Configuration.IncludedCandidates.ShouldBe(
            new[] { "block", "flex" });
        result.Configuration.Directives.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_Cancellation_ThrowsExpectedControlFlow()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilitySourceParser.Parse(
                """@source inline("{1..100}");""",
                null,
                source.Token));
    }

    [Fact]
    public void Parse_InvalidOptions_RejectBeforeScanning()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => UtilitySourceParser.Parse(
                string.Empty,
                new UtilitySourceParseOptions
                {
                    ContentOffset = -1,
                }));
        Should.Throw<ArgumentOutOfRangeException>(
            () => UtilitySourceParser.Parse(
                string.Empty,
                new UtilitySourceParseOptions
                {
                    MaximumExpansionCount = 0,
                }));
    }
}
