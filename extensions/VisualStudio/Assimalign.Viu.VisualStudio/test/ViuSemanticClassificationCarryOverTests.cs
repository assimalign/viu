using System;
using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins what survives being carried onto text the server has not answered for. A publication stops
/// describing the buffer at the first keystroke, and dropping it there is what repainted a script
/// block while it was being typed in: with no exact answer the lexical PascalCase-is-a-type guess
/// is all that remains.
/// </summary>
public class ViuSemanticClassificationCarryOverTests
{
    // "public string Title" on the second line of a two-line text.
    private static readonly IReadOnlyList<int> PublishedLineStarts = [0, 10];

    private static ViuSemanticClassification Classification(
        int line,
        int startCharacter,
        int endCharacter,
        string name = "property name") =>
        new(line, startCharacter, line, endCharacter, name);

    [Fact]
    public void Translate_TextInsertedBeforeASpan_MovesTheSpanWithIt()
    {
        // The word did not change, so neither should its color; only where it sits moved.
        var carried = ViuSemanticClassificationCarryOver.Translate(
            [Classification(1, 4, 9)],
            PublishedLineStarts,
            [0, 10],
            offset => offset + 3);

        var only = carried.ShouldHaveSingleItem();
        only.StartLine.ShouldBe(1);
        only.StartCharacter.ShouldBe(7);
        only.EndCharacter.ShouldBe(12);
        only.ClassificationTypeName.ShouldBe("property name");
    }

    [Fact]
    public void Translate_LinesInsertedAbove_ReportsTheSpanOnItsNewLine()
    {
        // A line start moves the whole span to a different line number; carrying it forward is what
        // keeps the rest of the document coloured while a line is being added at the top.
        var carried = ViuSemanticClassificationCarryOver.Translate(
            [Classification(1, 4, 9)],
            PublishedLineStarts,
            [0, 6, 16],
            offset => offset + 6);

        var only = carried.ShouldHaveSingleItem();
        only.StartLine.ShouldBe(2);
        only.StartCharacter.ShouldBe(4);
        only.EndCharacter.ShouldBe(9);
    }

    [Fact]
    public void Translate_SpanTheEditConsumed_IsDropped()
    {
        // The word the caret is inside is the one the server can no longer vouch for.
        ViuSemanticClassificationCarryOver.Translate(
                [Classification(1, 4, 9)],
                PublishedLineStarts,
                [0, 10],
                static _ => -1)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Translate_SpanCollapsedToNothing_IsDropped()
    {
        ViuSemanticClassificationCarryOver.Translate(
                [Classification(1, 4, 9)],
                PublishedLineStarts,
                [0, 10],
                static _ => 12)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Translate_SpanSplitAcrossALineBreak_IsDropped()
    {
        // A classification names a single-line range, and half of one is not a smaller truth: the
        // edit put a line break through the word, so nothing here is still known about it.
        ViuSemanticClassificationCarryOver.Translate(
                [Classification(1, 4, 9)],
                PublishedLineStarts,
                [0, 10, 16],
                offset => offset)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Translate_ClassificationOutsideThePublishedText_IsDropped()
    {
        ViuSemanticClassificationCarryOver.Translate(
                [Classification(7, 4, 9)],
                PublishedLineStarts,
                [0, 10],
                offset => offset)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Translate_InvalidClassification_IsDropped() =>
        ViuSemanticClassificationCarryOver.Translate(
                [new ViuSemanticClassification(1, 9, 1, 4, "property name")],
                PublishedLineStarts,
                [0, 10],
                offset => offset)
            .ShouldBeEmpty();

    [Fact]
    public void Translate_MissingArgument_Throws() =>
        Should.Throw<ArgumentNullException>(
            () => ViuSemanticClassificationCarryOver.Translate(
                [],
                PublishedLineStarts,
                [0, 10],
                translateOffset: null!));
}
