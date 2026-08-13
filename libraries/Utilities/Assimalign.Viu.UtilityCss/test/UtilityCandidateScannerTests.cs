using System;
using System.Linq;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.UtilityCss.Tests;

public sealed class UtilityCandidateScannerTests
{
    [Fact]
    public void Scan_StaticClassAttribute_DetectsCompleteCandidates()
    {
        // Tailwind CSS v4.3.3 compatibility contract:
        // crates/oxide/src/extractor/mod.rs, HTML candidate extraction vectors.
        const string text =
            """<main class="flex hidden hover:bg-blue-500/50!"></main>""";

        var result = UtilityCandidateScanner.Scan(text);

        result.IsSuccess.ShouldBeTrue();
        CandidateTexts(result).ShouldContain("flex");
        CandidateTexts(result).ShouldContain("hidden");
        CandidateTexts(result).ShouldContain("hover:bg-blue-500/50!");
    }

    [Fact]
    public void Scan_NonClassPlainText_DetectsCompleteCandidateAnywhereInRegion()
    {
        const string text =
            """registeredUtility = "grid-cols-3"; another-token""";

        var result = UtilityCandidateScanner.Scan(text);

        CandidateTexts(result).ShouldContain("grid-cols-3");
        CandidateTexts(result).ShouldContain("another-token");
    }

    [Fact]
    public void Scan_BoundStringArrayAndObjectLiterals_DetectsLiteralAlternatives()
    {
        const string text =
            """
            <div :class="condition
                ? ['flex', 'sm:hover:bg-red-500/50!']
                : { hidden: isHidden, 'content-["hello world"]': hasLabel }">
            </div>
            """;

        var result = UtilityCandidateScanner.Scan(text);

        CandidateTexts(result).ShouldContain("flex");
        CandidateTexts(result).ShouldContain("sm:hover:bg-red-500/50!");
        CandidateTexts(result).ShouldContain("hidden");
        CandidateTexts(result).ShouldContain("""content-["hello world"]""");
    }

    [Fact]
    public void Scan_ArbitraryValuesAndSelectorsWithWhitespaceAndQuotes_PreservesWholeTokens()
    {
        const string text =
            """
            [&:has(> img[alt='hero image'])]:block
            content-['hello world']
            bg-[url("data:image/svg+xml;utf8,<svg viewBox='0 0 1 1'>")]
            """;

        var result = UtilityCandidateScanner.Scan(text);

        var selector = FindCandidate(
            result,
            "[&:has(> img[alt='hero image'])]:block");
        selector.Candidate.Variants.ShouldHaveSingleItem().Selector.ShouldBe(
            "&:has(> img[alt='hero image'])");

        var content = FindCandidate(
            result,
            "content-['hello world']");
        content.Candidate.Value.ShouldNotBeNull();
        content.Candidate.Value.Text.ShouldBe("'hello world'");

        FindCandidate(
                result,
                """bg-[url("data:image/svg+xml;utf8,<svg viewBox='0 0 1 1'>")]""")
            .Candidate.Value.ShouldNotBeNull();
    }

    [Fact]
    public void Scan_DuplicateCandidates_SortsDistinctEntriesAndPreservesSourceOrder()
    {
        const string text = "z-10 flex z-10 block flex";

        var first = UtilityCandidateScanner.Scan(text);
        var second = UtilityCandidateScanner.Scan(text);

        CandidateTexts(first).ShouldBe(
            new[]
            {
                "block",
                "flex",
                "z-10",
            });
        FindCandidate(first, "flex").SourceSpans.Select(span => span.Start)
            .ShouldBe(new[] { 5, 21 });
        FindCandidate(first, "z-10").SourceSpans.Select(span => span.Start)
            .ShouldBe(new[] { 0, 10 });
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Scan_InterpolatedAndConcatenatedFragments_RejectsEveryIncompleteToken()
    {
        const string text =
            """
            `bg-${color}-500`
            bg-{tone}-500
            'text-' + color + '-600'
            flex
            """;

        var result = UtilityCandidateScanner.Scan(text);
        var candidates = CandidateTexts(result);

        candidates.ShouldContain("flex");
        candidates.ShouldNotContain("bg-${color}-500");
        candidates.ShouldNotContain("bg-{tone}-500");
        candidates.ShouldNotContain("text-");
        candidates.ShouldNotContain("-600");
        result.Diagnostics.Count(
                diagnostic =>
                    diagnostic.Code ==
                    UtilityCandidateScanDiagnosticCode.DynamicInterpolation)
            .ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Scan_MalformedCandidate_ReportsDiagnosticAndRecoversAtNextToken()
    {
        const string text = "bg-[] flex bg-[#123 block";

        var result = UtilityCandidateScanner.Scan(text);

        result.IsSuccess.ShouldBeFalse();
        CandidateTexts(result).ShouldContain("flex");
        CandidateTexts(result).ShouldContain("block");
        result.Diagnostics.ShouldContain(
            diagnostic =>
                diagnostic.Code ==
                    UtilityCandidateScanDiagnosticCode.CandidateParserDiagnostic &&
                diagnostic.CandidateDiagnosticCode ==
                    UtilityCandidateDiagnosticCode.InvalidArbitraryValue);
        result.Diagnostics.ShouldContain(
            diagnostic =>
                diagnostic.Code ==
                    UtilityCandidateScanDiagnosticCode.CandidateParserDiagnostic &&
                diagnostic.CandidateDiagnosticCode ==
                    UtilityCandidateDiagnosticCode.UnbalancedDelimiter);
    }

    [Fact]
    public void Scan_SourceIdentityAndContentOffset_MapEveryDuplicateSpanExactly()
    {
        const string text = """<div class="flex flex"></div>""";
        var options = new UtilityCandidateScanOptions
        {
            SourceIdentity = "Components/Card.vue",
            ContentOffset = 120,
        };

        var result = UtilityCandidateScanner.Scan(
            text,
            options);

        var expectedFirstStart =
            120 + text.IndexOf("flex", StringComparison.Ordinal);
        var flex = FindCandidate(result, "flex");
        flex.SourceSpans.Count.ShouldBe(2);
        flex.SourceSpans[0].ShouldBe(
            new UtilityCandidateSourceSpan(
                "Components/Card.vue",
                expectedFirstStart,
                4));
        flex.SourceSpans[1].Start.ShouldBe(expectedFirstStart + 5);
    }

    [Fact]
    public void FindTokenAtPosition_BalancedArbitrarySelectorWhitespace_DoesNotSplitToken()
    {
        const string candidate = "[&:has(> img[alt='hero image'])]:block";
        var text = "<div class=\"" + candidate + " flex\"></div>";
        var position =
            text.IndexOf("hero image", StringComparison.Ordinal) +
            "hero ".Length;
        var options = new UtilityCandidateScanOptions
        {
            SourceIdentity = "Index.html",
            ContentOffset = 30,
        };

        var token = UtilityCandidateScanner.FindTokenAtPosition(
            text,
            position,
            options);

        token.ShouldNotBeNull();
        token.Text.ShouldBe(candidate);
        token.Prefix.ShouldBe(
            candidate.Substring(
                0,
                position - text.IndexOf(candidate, StringComparison.Ordinal)));
        token.SourceSpan.ShouldBe(
            new UtilityCandidateSourceSpan(
                "Index.html",
                30 + text.IndexOf(candidate, StringComparison.Ordinal),
                candidate.Length));
    }

    [Fact]
    public void FindTokenAtPosition_BoundLiteralAndObjectKey_ReturnsClassContextsOnly()
    {
        const string text =
            """<div :class="condition ? 'hover:bg-red-500/50!' : { hidden: value }"></div>""";
        var literalPosition =
            text.IndexOf("bg-red", StringComparison.Ordinal) +
            "bg".Length;
        var objectPosition =
            text.IndexOf("hidden", StringComparison.Ordinal) +
            "hidden".Length;

        var literal = UtilityCandidateScanner.FindTokenAtPosition(
            text,
            literalPosition);
        var objectKey = UtilityCandidateScanner.FindTokenAtPosition(
            text,
            objectPosition);

        literal.ShouldNotBeNull();
        literal.Text.ShouldBe("hover:bg-red-500/50!");
        objectKey.ShouldNotBeNull();
        objectKey.Text.ShouldBe("hidden");
        UtilityCandidateScanner.FindTokenAtPosition(
                text,
                text.IndexOf("condition", StringComparison.Ordinal) + 2)
            .ShouldBeNull();
    }

    [Fact]
    public void FindTokenAtPosition_WhitespaceBetweenStaticClasses_ReturnsEmptyToken()
    {
        const string text = """<div class="flex  hidden"></div>""";
        var position =
            text.IndexOf("hidden", StringComparison.Ordinal) -
            1;

        var token = UtilityCandidateScanner.FindTokenAtPosition(
            text,
            position);

        token.ShouldNotBeNull();
        token.Text.ShouldBeEmpty();
        token.Prefix.ShouldBeEmpty();
        token.SourceSpan.Start.ShouldBe(position);
        token.SourceSpan.Length.ShouldBe(0);
    }

    [Fact]
    public void Scan_CanceledToken_ThrowsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Should.Throw<OperationCanceledException>(
            () => UtilityCandidateScanner.Scan(
                """<div class="flex"></div>""",
                UtilityCandidateScanOptions.Default,
                source.Token));
        Should.Throw<OperationCanceledException>(
            () => UtilityCandidateScanner.FindTokenAtPosition(
                """<div class="flex"></div>""",
                13,
                UtilityCandidateScanOptions.Default,
                source.Token));
    }

    private static string[] CandidateTexts(
        UtilityCandidateScanResult result) =>
        result.Candidates
            .Select(detection => detection.Candidate.RawText)
            .ToArray();

    private static UtilityCandidateDetection FindCandidate(
        UtilityCandidateScanResult result,
        string rawText) =>
        result.Candidates.Single(
            detection =>
                string.Equals(
                    detection.Candidate.RawText,
                    rawText,
                    StringComparison.Ordinal));
}
