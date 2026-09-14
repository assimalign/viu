using System;
using System.Collections.Generic;
using System.IO;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins when <c>Tab</c> means "expand the shortcut I just typed". The key belongs to the author, so
/// it only means expansion for a bare word that is a shipped shortcut, in the one section where the
/// snippets it opens are legal C#. Specified by [V01.01.12.29] (#344).
/// </summary>
public class ViuSnippetShortcutTests
{
    // Keep decision fixtures independent of future shipped additions, including differently cased
    // shortcuts. Only the expansion test below needs the actual catalog to pin the shipped prop.
    private static readonly ISet<string> Shortcuts = new HashSet<string>(StringComparer.Ordinal) { "prop" };

    private static readonly string[] Script = ["@script {", "    prop", "}"];

    [Fact]
    public void Find_ShortcutBeforeTheCaretInAScriptBlock_IsExpanded()
    {
        ViuSnippetShortcut.Find(Script, 1, "    prop".Length, ReadShippedShortcuts(), out int start)
            .ShouldBe("prop");
        start.ShouldBe(4);
    }

    [Fact]
    public void Find_ShortcutInATemplate_IsNotExpanded()
    {
        // The snippets are C# declarations; inserting one into markup would write text that cannot
        // parse, which is the same section restriction the brace expansion carries.
        string[] lines = ["<template>", "    prop", "</template>"];

        ViuSnippetShortcut.Find(lines, 1, "    prop".Length, Shortcuts, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_ShortcutInAStyleBlock_IsNotExpanded()
    {
        string[] lines = ["<style>", "    prop", "</style>"];

        ViuSnippetShortcut.Find(lines, 1, "    prop".Length, Shortcuts, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_CaretInsideTheWord_IsNotExpanded()
    {
        // A word continuing past the caret is one the author is still inside.
        ViuSnippetShortcut.Find(Script, 1, "    pro".Length, Shortcuts, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_WordThatIsNotAShippedShortcut_IsNotExpanded()
    {
        string[] lines = ["@script {", "    property", "}"];

        ViuSnippetShortcut.Find(lines, 1, "    property".Length, Shortcuts, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_NoWordBeforeTheCaret_IsNotExpanded()
    {
        // Tab at an indent is an indent, which is what it has always been.
        ViuSnippetShortcut.Find(Script, 1, 0, Shortcuts, out _).ShouldBeNull();
        ViuSnippetShortcut.Find(Script, 1, 2, Shortcuts, out _).ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 99)]
    public void Find_PositionOutsideTheDocument_IsNotExpanded(int lineNumber, int characterIndex) =>
        ViuSnippetShortcut.Find(Script, lineNumber, characterIndex, Shortcuts, out _).ShouldBeNull();

    [Theory]
    [InlineData("Prop")]
    [InlineData("PROP")]
    public void Find_DifferentCasing_IsNotExpanded(string word)
    {
        string[] lines = ["@script {", "    " + word, "}"];

        ViuSnippetShortcut.Find(lines, 1, lines[1].Length, Shortcuts, out _).ShouldBeNull();
    }

    [Fact]
    public void Find_AdditionalCatalogShortcut_IsExpandedWithoutADecisionLogicChange()
    {
        string[] lines = ["@script {", "    notify", "}"];
        var shortcuts = new HashSet<string>(StringComparer.Ordinal) { "notify" };

        ViuSnippetShortcut.Find(lines, 1, lines[1].Length, shortcuts, out int start)
            .ShouldBe("notify");
        start.ShouldBe(4);
    }

    [Fact]
    public void Find_EmptyCatalog_DoesNotExpandThePropertyShortcut()
    {
        var shortcuts = new HashSet<string>(StringComparer.Ordinal);

        ViuSnippetShortcut.Find(Script, 1, Script[1].Length, shortcuts, out _).ShouldBeNull();
    }

    private static ISet<string> ReadShippedShortcuts()
    {
        var failures = new List<string>();
        ISet<string> shortcuts = ViuSnippetCatalog.Read(
            Path.Combine(AppContext.BaseDirectory, "Snippets"), failures.Add);
        failures.ShouldBeEmpty();
        return shortcuts;
    }
}
