using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins the shape a <c>{ }</c> block takes when <c>Enter</c> expands it ([V01.01.12.07.09]): where the
/// two written lines are indented to, and whether the opening brace takes a line of its own.
/// </summary>
/// <remarks>
/// The <c>viu</c> content type has no smart-indent provider and no formatter, so the expansion
/// computes its own shape from the opening brace line and the buffer's tab options. These tests are
/// that computation's contract; the buffer edit that applies it is runtime-verified.
/// </remarks>
public class ViuBraceIndentationTests
{
    [Fact]
    public void ComputeBlockExpansion_SpaceIndentedOpeningLine_ClosesUnderItAndIndentsTheCaretOneLevel()
    {
        // The C# shape for an empty block: the closer lines up with the construct that opened it and
        // the caret sits one level in.
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            "    partial void OnSetup() {",
            openingBraceIndex: 27,
            indentSize: 4,
            convertTabsToSpaces: true);

        expansion.ClosingBraceIndentation.ShouldBe("    ");
        expansion.CaretIndentation.ShouldBe("        ");
    }

    [Fact]
    public void ComputeBlockExpansion_OpeningLineAtColumnZero_ClosesAtColumnZero()
    {
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            "@script {",
            openingBraceIndex: 8,
            indentSize: 4,
            convertTabsToSpaces: true);

        expansion.ClosingBraceIndentation.ShouldBe(string.Empty);
        expansion.CaretIndentation.ShouldBe("    ");
    }

    [Fact]
    public void ComputeBlockExpansion_NestedOpeningLine_AddsExactlyOneLevel()
    {
        // Nesting is carried entirely by the opening line's own indentation, which is why no brace
        // counting is needed to place a block correctly at any depth.
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            "        if (Visible) {",
            openingBraceIndex: 21,
            indentSize: 4,
            convertTabsToSpaces: true);

        expansion.ClosingBraceIndentation.ShouldBe("        ");
        expansion.CaretIndentation.ShouldBe("            ");
    }

    [Fact]
    public void ComputeBlockExpansion_IndentSizeTwo_AddsTwoSpaces()
    {
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            "  var handler = new Action(() => {",
            openingBraceIndex: 33,
            indentSize: 2,
            convertTabsToSpaces: true);

        expansion.ClosingBraceIndentation.ShouldBe("  ");
        expansion.CaretIndentation.ShouldBe("    ");
    }

    [Fact]
    public void ComputeBlockExpansion_TabIndentedBuffer_AddsOneTabWhateverTheIndentSizeSays()
    {
        // A tab is one level by definition, so the indent size has nothing to say about its width.
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            "\t\tpartial void OnSetup() {",
            openingBraceIndex: 25,
            indentSize: 4,
            convertTabsToSpaces: false);

        expansion.ClosingBraceIndentation.ShouldBe("\t\t");
        expansion.CaretIndentation.ShouldBe("\t\t\t");
    }

    [Fact]
    public void ComputeBlockExpansion_ExistingWhitespaceThatDisagreesWithTheOption_IsCopiedNotRewritten()
    {
        // Recorded decision ([V01.01.12.07.09]): the copied indentation is verbatim. Normalizing it
        // would silently reformat a line the user did not touch; only the added level follows the
        // buffer's option.
        ViuBraceIndentation.ComputeBlockExpansion(
                "\tif (Visible) {",
                openingBraceIndex: 14,
                indentSize: 4,
                convertTabsToSpaces: true)
            .ShouldBe(new ViuBlockExpansion("\t", "\t    ", 13));

        ViuBraceIndentation.ComputeBlockExpansion(
                "    if (Visible) {",
                openingBraceIndex: 17,
                indentSize: 4,
                convertTabsToSpaces: false)
            .ShouldBe(new ViuBlockExpansion("    ", "    \t", 16));
    }

    [Fact]
    public void ComputeBlockExpansion_MixedLeadingWhitespace_IsCopiedWhole()
    {
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            "\t  if (Visible) {",
            openingBraceIndex: 16,
            indentSize: 4,
            convertTabsToSpaces: true);

        expansion.ClosingBraceIndentation.ShouldBe("\t  ");
        expansion.CaretIndentation.ShouldBe("\t      ");
    }

    [Fact]
    public void ComputeBlockExpansion_IndentSizeBelowOne_StillIndentsOneSpace()
    {
        // A zero-width level would leave the caret's blank line indistinguishable from the closer's,
        // which defeats the expansion; one space is the floor ([V01.01.12.07.09]).
        ViuBraceIndentation.ComputeBlockExpansion(
                "    void M() {",
                openingBraceIndex: 13,
                indentSize: 0,
                convertTabsToSpaces: true)
            .CaretIndentation.ShouldBe("     ");
        ViuBraceIndentation.ComputeBlockExpansion(
                "    void M() {",
                openingBraceIndex: 13,
                indentSize: -3,
                convertTabsToSpaces: true)
            .CaretIndentation.ShouldBe("     ");
    }

    [Fact]
    public void ComputeBlockExpansion_EmptyOpeningLine_IndentsFromColumnZero()
    {
        ViuBraceIndentation.ComputeBlockExpansion(
                string.Empty,
                openingBraceIndex: -1,
                indentSize: 4,
                convertTabsToSpaces: true)
            .ShouldBe(new ViuBlockExpansion(string.Empty, "    ", -1));
    }

    [Fact]
    public void ComputeBlockExpansion_BraceAfterADeclaration_MovesItOntoItsOwnLine()
    {
        // The reported defect: the block opened as "void Test(){" with the caret below it, a shape no
        // C# in the file is written in. The move starts at the whitespace before the brace, so the
        // declaration is not left with a trailing space.
        ViuBraceIndentation.ComputeBlockExpansion(
                "    public void Test() {",
                openingBraceIndex: 23,
                indentSize: 4,
                convertTabsToSpaces: true)
            .OpeningBraceReplaceStart.ShouldBe(22);

        ViuBraceIndentation.ComputeBlockExpansion(
                "    public void Test(){",
                openingBraceIndex: 22,
                indentSize: 4,
                convertTabsToSpaces: true)
            .OpeningBraceReplaceStart.ShouldBe(22);
    }

    [Fact]
    public void ComputeBlockExpansion_BraceThatAlreadyBeginsItsLine_StaysWhereItIs()
    {
        // Nothing to move, and saying so is what keeps a second Enter inside an expanded block from
        // rewriting the brace it is already nested under.
        ViuBraceIndentation.ComputeBlockExpansion(
                "    {",
                openingBraceIndex: 4,
                indentSize: 4,
                convertTabsToSpaces: true)
            .OpeningBraceReplaceStart.ShouldBe(-1);

        ViuBraceIndentation.ComputeBlockExpansion(
                "{",
                openingBraceIndex: 0,
                indentSize: 4,
                convertTabsToSpaces: true)
            .OpeningBraceReplaceStart.ShouldBe(-1);
    }

    [Fact]
    public void ComputeBlockExpansion_BraceIndexOutsideTheLine_LeavesTheBraceAlone()
    {
        ViuBraceIndentation.ComputeBlockExpansion(
                "    void M() {",
                openingBraceIndex: 99,
                indentSize: 4,
                convertTabsToSpaces: true)
            .OpeningBraceReplaceStart.ShouldBe(-1);
    }

    [Fact]
    public void ReadLeadingWhitespace_LineThatIsAllWhitespace_ReturnsTheWholeLine()
    {
        ViuBraceIndentation.ReadLeadingWhitespace("    ").ShouldBe("    ");
        ViuBraceIndentation.ReadLeadingWhitespace(string.Empty).ShouldBe(string.Empty);
        ViuBraceIndentation.ReadLeadingWhitespace("void M() {").ShouldBe(string.Empty);
    }

    [Fact]
    public void ReadLeadingWhitespace_InteriorWhitespace_IsNotLeading()
    {
        ViuBraceIndentation.ReadLeadingWhitespace("  var a = new [] { 1, 2 };").ShouldBe("  ");
    }
}
