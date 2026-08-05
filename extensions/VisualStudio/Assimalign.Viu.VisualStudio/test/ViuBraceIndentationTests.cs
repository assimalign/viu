using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins the indentation a <c>{ }</c> block takes when <c>Enter</c> expands it ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// The <c>viu</c> content type has no smart-indent provider, so the expansion computes its own
/// indentation from the opening brace line and the buffer's tab options. These tests are that
/// computation's contract; the buffer edit that applies the two strings is runtime-verified.
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
        ViuBraceIndentation.ComputeBlockExpansion("\tif (Visible) {", indentSize: 4, convertTabsToSpaces: true)
            .ShouldBe(new ViuBlockExpansion("\t", "\t    "));

        ViuBraceIndentation.ComputeBlockExpansion("    if (Visible) {", indentSize: 4, convertTabsToSpaces: false)
            .ShouldBe(new ViuBlockExpansion("    ", "    \t"));
    }

    [Fact]
    public void ComputeBlockExpansion_MixedLeadingWhitespace_IsCopiedWhole()
    {
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            "\t  if (Visible) {",
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
        ViuBraceIndentation.ComputeBlockExpansion("    void M() {", indentSize: 0, convertTabsToSpaces: true)
            .CaretIndentation.ShouldBe("     ");
        ViuBraceIndentation.ComputeBlockExpansion("    void M() {", indentSize: -3, convertTabsToSpaces: true)
            .CaretIndentation.ShouldBe("     ");
    }

    [Fact]
    public void ComputeBlockExpansion_EmptyOpeningLine_IndentsFromColumnZero()
    {
        ViuBraceIndentation.ComputeBlockExpansion(string.Empty, indentSize: 4, convertTabsToSpaces: true)
            .ShouldBe(new ViuBlockExpansion(string.Empty, "    "));
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
