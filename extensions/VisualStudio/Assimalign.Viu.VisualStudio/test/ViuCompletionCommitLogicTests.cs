using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins how an attribute completion commits: the caret lands in the value, and the replaced span is
/// the attribute name being typed rather than the word the editor would infer.
/// </summary>
public class ViuCompletionCommitLogicTests
{
    [Theory]
    // The shorthand is part of the name. Inferring the span from the typed word leaves it behind and
    // commits beside it, which is where ::title and @@submitted came from.
    [InlineData("    <Button :", 13, ":title=\"\"", 12, 1, "    <Button :title=\"\"")]
    [InlineData("    <Button @sub", 16, "@submitted=\"\"", 12, 4, "    <Button @submitted=\"\"")]
    [InlineData("    <div v-i", 12, "v-if=\"\"", 9, 3, "    <div v-if=\"\"")]
    // Nothing typed: the commit inserts and consumes no text.
    [InlineData("    <div ", 9, "v-if=\"\"", 9, 0, "    <div v-if=\"\"")]
    public void GetAttributeCommitEdit_AttributeItem_ReplacesTheWholeNameBeingTyped(
        string line,
        int caretIndex,
        string insertText,
        int expectedStart,
        int expectedLength,
        string expectedLine)
    {
        var edit = ViuCompletionCommitLogic.GetAttributeCommitEdit(line, caretIndex, insertText);

        edit.ShouldNotBeNull();
        edit!.ReplaceStart.ShouldBe(expectedStart);
        edit.ReplaceLength.ShouldBe(expectedLength);
        var committed = line.Substring(0, edit.ReplaceStart) +
            edit.Text +
            line.Substring(edit.ReplaceStart + edit.ReplaceLength);
        committed.ShouldBe(expectedLine);
    }

    [Fact]
    public void GetAttributeCommitEdit_AttributeItem_LandsTheCaretBetweenTheQuotes()
    {
        // The value is where the author is going, and only a snippet tabstop could ask for it - which
        // this editor does not expand, so the caret would otherwise land past the closing quote.
        const string line = "    <div v-i";
        var edit = ViuCompletionCommitLogic.GetAttributeCommitEdit(line, line.Length, "v-if=\"\"");

        edit.ShouldNotBeNull();
        var committed = line.Substring(0, edit!.ReplaceStart) + edit.Text;
        committed.ShouldBe("    <div v-if=\"\"");
        var caret = edit.ReplaceStart + edit.CaretOffset;
        committed.Substring(0, caret).ShouldEndWith("v-if=\"");
        committed.Substring(caret).ShouldBe("\"");
    }

    [Fact]
    public void GetAttributeCommitEdit_NameBeforeAClosingAngle_LeavesTheAngleAlone()
    {
        // The reported defect: committing an attribute typed against the closing '>' consumed it.
        // The replacement reaches back over the name and stops at the caret, never forward.
        const string line = "    <div class=\"card\" v-i>";
        var caret = line.IndexOf("v-i", System.StringComparison.Ordinal) + "v-i".Length;

        var edit = ViuCompletionCommitLogic.GetAttributeCommitEdit(line, caret, "v-if=\"\"");

        edit.ShouldNotBeNull();
        var committed = line.Substring(0, edit!.ReplaceStart) +
            edit.Text +
            line.Substring(edit.ReplaceStart + edit.ReplaceLength);
        committed.ShouldBe("    <div class=\"card\" v-if=\"\">");
    }

    [Theory]
    // An element, an expression member, and a valueless directive commit the way they always did.
    [InlineData("<div")]
    [InlineData("CurrentPath")]
    [InlineData("v-else")]
    [InlineData("v-bind:")]
    [InlineData("")]
    [InlineData(null)]
    public void GetAttributeCommitEdit_EverythingElse_Declines(string? insertText) =>
        ViuCompletionCommitLogic
            .GetAttributeCommitEdit("    <div ", 9, insertText)
            .ShouldBeNull();

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void GetAttributeCommitEdit_CaretOutsideTheLine_Declines(int caretIndex) =>
        ViuCompletionCommitLogic
            .GetAttributeCommitEdit("    <div ", caretIndex, "v-if=\"\"")
            .ShouldBeNull();
}
