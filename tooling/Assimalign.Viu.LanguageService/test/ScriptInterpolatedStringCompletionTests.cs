using System;
using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins completion gating across the literal and expression segments of interpolated strings.
/// Literal prose remains suppressed, while every interpolation hole is ordinary C# code even when
/// strings and further interpolation holes are nested inside it. [V01.01.12.07.14]
/// </summary>
public class ScriptInterpolatedStringCompletionTests
{
    private const string CaretMarker = "[|]";

    [Fact]
    public void GetCompletions_CursorInsideInterpolatedStringText_ReturnsNothing()
    {
        var completions = GetCompletions(
            "public string Message => $\"Literal Con[|] {Context}\";");

        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_CursorInsideInterpolationHole_OffersExpressionCompletions()
    {
        var completions = GetCompletions(
            "public string Message => $\"Literal {Con[|]}\";");

        completions.ShouldContain(completion => completion.Label == "Context");
    }

    [Fact]
    public void GetCompletions_CursorInsideStringNestedInInterpolationHole_ReturnsNothing()
    {
        var completions = GetCompletions(
            "public string Message => $\"Outer {string.Concat(\"Con[|]\")}\";");

        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_CursorInsideNestedInterpolationHole_OffersExpressionCompletions()
    {
        var completions = GetCompletions(
            "public string Message => $\"Outer {string.Concat($\"Inner {Con[|]}\")}\";");

        completions.ShouldContain(completion => completion.Label == "Context");
    }

    [Theory]
    [InlineData("@$")]
    [InlineData("$@")]
    public void GetCompletions_VerbatimInterpolatedHoleAfterDoubledQuote_OffersExpressionCompletions(
        string prefix)
    {
        var completions = GetCompletions(
            $"public string Message => {prefix}\"C:\\data\\ \"\"quoted\"\" {{Con[|]}}\";");

        completions.ShouldContain(completion => completion.Label == "Context");
    }

    [Theory]
    [InlineData("@$")]
    [InlineData("$@")]
    public void GetCompletions_VerbatimInterpolatedTextAfterDoubledQuote_ReturnsNothing(
        string prefix)
    {
        var completions = GetCompletions(
            $"public string Message => {prefix}\"Literal \"\"quoted\"\" Con[|] {{Context}}\";");

        completions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("@$")]
    [InlineData("$@")]
    public void GetCompletions_AfterClosedVerbatimInterpolatedString_OffersExpressionCompletions(
        string prefix)
    {
        var completions = GetCompletions(
            $"public string Message => {prefix}\"C:\\data\\\" + Con[|];");

        completions.ShouldContain(completion => completion.Label == "Context");
    }

    private static IReadOnlyList<LanguageCompletionItem> GetCompletions(
        string memberWithCaret)
    {
        var markedSource =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            memberWithCaret + "\n" +
            "}\n";
        var caretOffset = markedSource.IndexOf(CaretMarker, StringComparison.Ordinal);
        caretOffset.ShouldBeGreaterThanOrEqualTo(0);
        var source = markedSource.Remove(caretOffset, CaretMarker.Length);
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        return service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            TextCoordinateConverter.GetPosition(source, caretOffset));
    }
}
