using System.Collections.Generic;
using System.Text.Json;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio.Tests;

/// <summary>
/// Pins the editor-free portion of the #334 Visual Studio completion-swatch contract.
/// </summary>
public class ViuCompletionColorPresentationTests
{
    [Fact]
    public void TryParse_DefaultUtilityOklch_ProducesDeviceSrgbSwatch()
    {
        bool parsed = ViuCompletionColor.TryParse(
            "oklch(62.3% 0.214 259.815)",
            out ViuCompletionColor color);

        parsed.ShouldBeTrue();
        color.Red.ShouldBe((byte)43);
        color.Green.ShouldBe((byte)127);
        color.Blue.ShouldBe((byte)255);
        color.Alpha.ShouldBe((byte)255);
    }

    [Theory]
    [InlineData("#123456", 18, 52, 86, 255)]
    [InlineData("#3698", 51, 102, 153, 136)]
    [InlineData("rgb(25% 50% 100% / 40%)", 64, 128, 255, 102)]
    [InlineData("hsl(270 50% 40%)", 102, 51, 153, 255)]
    [InlineData("rebeccapurple", 102, 51, 153, 255)]
    public void TryParse_ConcreteCssColor_ProducesExpectedChannels(
        string value,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        bool parsed = ViuCompletionColor.TryParse(value, out ViuCompletionColor color);

        parsed.ShouldBeTrue();
        color.Red.ShouldBe(red);
        color.Green.ShouldBe(green);
        color.Blue.ShouldBe(blue);
        color.Alpha.ShouldBe(alpha);
    }

    [Theory]
    [InlineData("var(--application-brand)")]
    [InlineData("color-mix(in oklab, red 50%, blue)")]
    [InlineData("")]
    public void TryParse_NonConcreteCssValue_DoesNotInventSwatch(string value)
    {
        ViuCompletionColor.TryParse(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_CompletionResponse_CarriesCandidateSpecificColorAndSkipsOtherItems()
    {
        using JsonDocument parameters = JsonDocument.Parse(
            """
            {"textDocument":{"uri":"file:///C:/Source/Card.viu"}}
            """);
        using JsonDocument response = JsonDocument.Parse(
            """
            {
              "items": [
                {
                  "label": "bg-blue-500",
                  "kind": 16,
                  "data": {"colorValue": "oklch(62.3% 0.214 259.815)"}
                },
                {
                  "label": "block",
                  "kind": 21,
                  "data": {"label": "block"}
                }
              ]
            }
            """);

        bool parsed = ViuCompletionColorPublication.TryParse(
            parameters.RootElement,
            response.RootElement,
            out var publication);

        parsed.ShouldBeTrue();
        publication.ShouldNotBeNull();
        publication!.DocumentUri.ShouldBe("file:///C:/Source/Card.viu");
        publication.Presentations.Count.ShouldBe(1);
        var identity = new ViuCompletionCandidateIdentity(
            "bg-blue-500",
            "bg-blue-500",
            "bg-blue-500",
            "bg-blue-500");
        ViuCompletionColorPresentation presentation = publication.Presentations[identity];
        presentation.CssValue.ShouldBe("oklch(62.3% 0.214 259.815)");
        presentation.Color.ShouldBe(new ViuCompletionColor(43, 127, 255, 255));
    }

    [Fact]
    public void Publish_FileUri_CanBeReadByVisualStudioDocumentPathAndCleared()
    {
        using JsonDocument parameters = JsonDocument.Parse(
            """
            {"textDocument":{"uri":"file:///C:/Source/Card.viu"}}
            """);
        using JsonDocument response = JsonDocument.Parse(
            """
            [{"label":"text-red-500","data":{"colorValue":"#ef4444"}}]
            """);
        ViuCompletionColorPublication.TryParse(
            parameters.RootElement,
            response.RootElement,
            out var publication).ShouldBeTrue();
        var state = new ViuCompletionColorState();

        state.Publish(publication!);
        var identity = new ViuCompletionCandidateIdentity(
            "text-red-500",
            "text-red-500",
            "text-red-500",
            "text-red-500");
        IReadOnlyList<IReadOnlyList<ViuCompletionCandidateIdentity>> sourceGroups =
            new[] { new[] { identity } };
        bool found = state.TryTakeMatchingPublication(
            @"C:\Source\Card.viu",
            sourceGroups,
            out int sourceGroupIndex,
            out var matchedPublication);

        found.ShouldBeTrue();
        sourceGroupIndex.ShouldBe(0);
        matchedPublication.ShouldNotBeNull();
        matchedPublication!.Presentations[identity].Color.ShouldBe(
            new ViuCompletionColor(239, 68, 68, 255));

        state.Clear();
        state.TryTakeMatchingPublication(
            @"C:\Source\Card.viu",
            sourceGroups,
            out _,
            out _).ShouldBeFalse();
    }

    [Fact]
    public void TryTakeMatchingPublication_ConcurrentResponses_MatchesCompleteListIdentity()
    {
        ViuCompletionColorPublication first = ParsePublication(
            "bg-red-500",
            "bg-red-500",
            "#ef4444");
        ViuCompletionColorPublication second = ParsePublication(
            "bg-blue-500",
            "bg-blue-500",
            "#3b82f6");
        var state = new ViuCompletionColorState();
        state.Publish(first);
        state.Publish(second);
        IReadOnlyList<IReadOnlyList<ViuCompletionCandidateIdentity>> sourceGroups =
            new[] { first.Candidates };

        bool found = state.TryTakeMatchingPublication(
            @"C:\Source\Card.viu",
            sourceGroups,
            out int sourceGroupIndex,
            out var matchedPublication);

        found.ShouldBeTrue();
        sourceGroupIndex.ShouldBe(0);
        matchedPublication.ShouldBeSameAs(first);
    }

    [Fact]
    public void TryTakeMatchingPublication_SameLabelFromOtherSource_MatchesFullCandidateIdentity()
    {
        ViuCompletionColorPublication publication = ParsePublication(
            "bg-blue-500",
            "bg-blue-500",
            "#3b82f6");
        var foreignIdentity = new ViuCompletionCandidateIdentity(
            "bg-blue-500",
            "different-insert-text",
            "bg-blue-500",
            "bg-blue-500");
        var state = new ViuCompletionColorState();
        state.Publish(publication);
        IReadOnlyList<IReadOnlyList<ViuCompletionCandidateIdentity>> sourceGroups =
            new[] { new[] { foreignIdentity }, publication.Candidates };

        bool found = state.TryTakeMatchingPublication(
            @"C:\Source\Card.viu",
            sourceGroups,
            out int sourceGroupIndex,
            out var matchedPublication);

        found.ShouldBeTrue();
        sourceGroupIndex.ShouldBe(1);
        matchedPublication.ShouldBeSameAs(publication);
    }

    [Fact]
    public void Current_UntitledDocumentSavedAsFile_ReadsNewPathWithoutRecreatingManagerState()
    {
        string currentPath = string.Empty;
        var documentPath = new ViuCompletionDocumentPath(() => currentPath);

        documentPath.Current.ShouldBeEmpty();

        currentPath = @"C:\Source\SavedCard.viu";

        documentPath.Current.ShouldBe(@"C:\Source\SavedCard.viu");
    }

    private static ViuCompletionColorPublication ParsePublication(
        string label,
        string insertText,
        string colorValue)
    {
        using JsonDocument parameters = JsonDocument.Parse(
            """
            {"textDocument":{"uri":"file:///C:/Source/Card.viu"}}
            """);
        using JsonDocument response = JsonDocument.Parse(
            JsonSerializer.Serialize(
                new[]
                {
                    new
                    {
                        label,
                        insertText,
                        data = new { colorValue },
                    },
                }));
        ViuCompletionColorPublication.TryParse(
            parameters.RootElement,
            response.RootElement,
            out var publication).ShouldBeTrue();
        return publication!;
    }
}
