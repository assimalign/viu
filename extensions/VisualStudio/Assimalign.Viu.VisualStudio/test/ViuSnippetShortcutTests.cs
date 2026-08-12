using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins when <c>Tab</c> means "expand the shortcut I just typed". The key belongs to the author, so
/// it only means expansion for a bare word that is a shipped shortcut, in the one section where the
/// snippets it opens are legal C#.
/// </summary>
public class ViuSnippetShortcutTests
{
    private static readonly string[] Script = ["@script {", "    prop", "}"];

    [Fact]
    public void Find_ShortcutBeforeTheCaretInAScriptBlock_IsExpanded()
    {
        ViuSnippetShortcut.Find(Script, 1, "    prop".Length, out int start).ShouldBe("prop");
        start.ShouldBe(4);
    }

    [Fact]
    public void Find_ShortcutInATemplate_IsNotExpanded()
    {
        // The snippets are C# declarations; inserting one into markup would write text that cannot
        // parse, which is the same section restriction the brace expansion carries.
        string[] lines = ["<template>", "    prop", "</template>"];

        ViuSnippetShortcut.Find(lines, 1, "    prop".Length, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_ShortcutInAStyleBlock_IsNotExpanded()
    {
        string[] lines = ["<style>", "    prop", "</style>"];

        ViuSnippetShortcut.Find(lines, 1, "    prop".Length, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_CaretInsideTheWord_IsNotExpanded()
    {
        // A word continuing past the caret is one the author is still inside.
        ViuSnippetShortcut.Find(Script, 1, "    pro".Length, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_WordThatIsNotAShippedShortcut_IsNotExpanded()
    {
        string[] lines = ["@script {", "    property", "}"];

        ViuSnippetShortcut.Find(lines, 1, "    property".Length, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_NoWordBeforeTheCaret_IsNotExpanded()
    {
        // Tab at an indent is an indent, which is what it has always been.
        ViuSnippetShortcut.Find(Script, 1, 0, out _).ShouldBeNull();
        ViuSnippetShortcut.Find(Script, 1, 2, out _).ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 99)]
    public void Find_PositionOutsideTheDocument_IsNotExpanded(int lineNumber, int characterIndex) =>
        ViuSnippetShortcut.Find(Script, lineNumber, characterIndex, out _).ShouldBeNull();

    [Fact]
    public void All_ListsTheShippedShortcuts() =>
        // The list a new .snippet file has to join; adding one is that file plus this entry.
        ViuSnippetShortcut.All.ShouldBe(["prop"]);
}
