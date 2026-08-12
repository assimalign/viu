using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins what a tooltip is made of. Handing the server's Markdown to the host draws the fenced
/// declaration as a document artifact, copy button included, and nothing in a tooltip is meant to be
/// lifted out; rendering it here removes the decoration and colors the declaration, because the
/// fence's language says which grammar to color it with.
/// </summary>
public class ViuHoverPresentationTests
{
    [Fact]
    public void Create_CSharpDeclaration_ColorsItWithTheSamePassThatColorsTheDocument()
    {
        var lines = ViuHoverPresentation.Create("```csharp\npublic int Count\n```\nA counter.");

        lines.Count.ShouldBe(2);
        var declaration = lines[0];
        Text(declaration).ShouldBe("public int Count");
        // A signature reads in the tooltip exactly as it reads in the file.
        Classification(declaration, "public").ShouldBe(ViuClassificationTypeNames.Keyword);
        Classification(declaration, "int").ShouldBe(ViuClassificationTypeNames.Keyword);
        Classification(declaration, "Count").ShouldBe(ViuClassificationTypeNames.ClassName);
    }

    [Fact]
    public void Create_CssDeclaration_IsColoredAsStyleNotAsCSharp()
    {
        // The same tooltip body serves both grammars, which is the whole reason the fence carries a
        // language: colouring a CSS rule as C# would read as nonsense.
        var lines = ViuHoverPresentation.Create("`.card` — declared here.\n\n```css\ncolor: red;\n```");

        var declaration = lines[^1];
        Text(declaration).ShouldBe("color: red;");
        Classification(declaration, "color").ShouldBe(ViuClassificationTypeNames.Attribute);
    }

    [Fact]
    public void Create_Prose_ReadsAsBodyTextAndDropsItsBackticks()
    {
        var lines = ViuHoverPresentation.Create("```csharp\nclass Card\n```\nResolves the `<Card>` tag.");

        var prose = lines[^1];
        Text(prose).ShouldBe("Resolves the <Card> tag.");
        prose[0].ClassificationTypeName.ShouldBe(ViuClassificationTypeNames.Text);
        // A name in prose is marked as one, not offered as code.
        Classification(prose, "<Card>").ShouldBe(ViuClassificationTypeNames.Identifier);
    }

    [Fact]
    public void Create_MultiLineDeclaration_KeepsOneLinePerDeclaredMember()
    {
        var lines = ViuHoverPresentation.Create(
            "```csharp\nclass FeatureCard\n    string Title\n```\nResolves the tag.");

        lines.Count.ShouldBe(3);
        Text(lines[0]).ShouldBe("class FeatureCard");
        Text(lines[1]).ShouldBe("    string Title");
    }

    [Fact]
    public void Create_UnterminatedFence_StillShowsTheDeclaration()
    {
        // Losing a whole signature over a missing marker would be worse than rendering it.
        var lines = ViuHoverPresentation.Create("```csharp\npublic int Count");

        Text(lines.ShouldHaveSingleItem()).ShouldBe("public int Count");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NothingToShow_ProducesNoLines(string? markdown) =>
        ViuHoverPresentation.Create(markdown).ShouldBeEmpty();

    private static string Text(IReadOnlyList<ViuHoverRun> runs) =>
        string.Concat(runs.Select(run => run.Text));

    private static string Classification(IReadOnlyList<ViuHoverRun> runs, string text) =>
        runs.First(run => run.Text == text).ClassificationTypeName;
}
