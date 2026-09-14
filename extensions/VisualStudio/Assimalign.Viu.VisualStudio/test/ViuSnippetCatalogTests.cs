using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins the file-owned shortcut catalog and its all-or-empty failure contract without an editor.
/// Specified by [V01.01.12.29] (#344).
/// </summary>
public class ViuSnippetCatalogTests
{
    [Fact]
    public void Read_OneFile_ReadsTheHeaderShortcutAndDisposesTheStream()
    {
        var failures = new List<string>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(CreateSnippet("notify")));

        ISet<string> shortcuts = ViuSnippetCatalog.Read(
            "Snippets", failures.Add, _ => ["notify.snippet"], _ => stream);

        shortcuts.ShouldBe(["notify"]);
        failures.ShouldBeEmpty();
        stream.CanRead.ShouldBeFalse();
    }

    [Fact]
    public void Read_SeveralFiles_CombinesDistinctOrdinalShortcuts()
    {
        var failures = new List<string>();
        var files = new Dictionary<string, string>
        {
            ["property.snippet"] = CreateSnippet("prop"),
            ["notification.snippet"] = CreateSnippet("notify"),
            ["duplicate.snippet"] = CreateSnippet("prop"),
            ["uppercase.snippet"] = CreateSnippet("PROP"),
        };

        ISet<string> shortcuts = ReadFiles(files, failures);

        shortcuts.OrderBy(shortcut => shortcut, StringComparer.Ordinal)
            .ShouldBe(["PROP", "notify", "prop"]);
        shortcuts.Contains("Prop").ShouldBeFalse();
        failures.ShouldBeEmpty();
    }

    [Fact]
    public void Read_SeveralSnippetsWithNamespacePrefixes_ReadsOnlyHeaderShortcuts()
    {
        const string content = """
            <s:CodeSnippets xmlns:s="http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet">
              <s:CodeSnippet Format="1.0.0">
                <s:Header><s:Shortcut>first</s:Shortcut></s:Header>
                <s:Snippet><s:Shortcut>ignored</s:Shortcut></s:Snippet>
              </s:CodeSnippet>
              <s:CodeSnippet Format="1.0.0">
                <s:Header><s:Shortcut>second</s:Shortcut></s:Header>
              </s:CodeSnippet>
            </s:CodeSnippets>
            """;
        var failures = new List<string>();

        ISet<string> shortcuts = ReadFiles(new() { ["several.snippet"] = content }, failures);

        shortcuts.OrderBy(shortcut => shortcut, StringComparer.Ordinal).ShouldBe(["first", "second"]);
        failures.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("<Shortcut />")]
    [InlineData("<Shortcut>  </Shortcut>")]
    public void Read_FileWithoutAShortcut_DiscardsEarlierShortcutsAndLogsTheFile(string header)
    {
        var failures = new List<string>();
        string missingShortcut = CreateSnippet("unused")
            .Replace("<Shortcut>unused</Shortcut>", header);

        ISet<string> shortcuts = ReadFiles(new()
        {
            ["first.snippet"] = CreateSnippet("prop"),
            ["missing.snippet"] = missingShortcut,
        }, failures);

        shortcuts.ShouldBeEmpty();
        failures.Count.ShouldBe(1);
        failures[0].ShouldContain("missing.snippet");
        failures[0].ShouldContain("Header/Shortcut");
    }

    [Theory]
    [InlineData("<CodeSnippets>")]
    [InlineData("<CodeSnippets />")]
    [InlineData("<Shortcut>prop</Shortcut>")]
    public void Read_InvalidFile_DiscardsEarlierShortcutsAndDisposesTheStream(string content)
    {
        var failures = new List<string>();
        var validStream = new MemoryStream(Encoding.UTF8.GetBytes(CreateSnippet("prop")));
        var invalidStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        ISet<string> shortcuts = ViuSnippetCatalog.Read(
            "Snippets", failures.Add,
            _ => ["first.snippet", "invalid.snippet"],
            path => path == "first.snippet" ? validStream : invalidStream);

        shortcuts.ShouldBeEmpty();
        failures.Count.ShouldBe(1);
        failures[0].ShouldContain("invalid.snippet");
        validStream.CanRead.ShouldBeFalse();
        invalidStream.CanRead.ShouldBeFalse();
    }

    [Fact]
    public void Read_MalformedXmlAfterAValidShortcut_ReturnsNoShortcuts()
    {
        var failures = new List<string>();

        ISet<string> shortcuts = ReadFiles(new()
        {
            ["trailing.snippet"] = CreateSnippet("prop") + "<",
        }, failures);

        shortcuts.ShouldBeEmpty();
        failures.Count.ShouldBe(1);
        failures[0].ShouldContain("trailing.snippet");
    }

    [Fact]
    public void Read_UnreadableFile_DiscardsEarlierShortcutsAndLogsTheFile()
    {
        var failures = new List<string>();

        ISet<string> shortcuts = ViuSnippetCatalog.Read(
            "Snippets", failures.Add,
            _ => ["first.snippet", "unreadable.snippet"],
            path => path == "first.snippet"
                ? new MemoryStream(Encoding.UTF8.GetBytes(CreateSnippet("prop")))
                : throw new UnauthorizedAccessException("Access denied."));

        shortcuts.ShouldBeEmpty();
        failures.Count.ShouldBe(1);
        failures[0].ShouldContain("unreadable.snippet");
        failures[0].ShouldContain("Access denied.");
    }

    [Fact]
    public void Read_EnumerationFailsAfterAFile_DiscardsEarlierShortcutsAndLogsTheDirectory()
    {
        var failures = new List<string>();

        ISet<string> shortcuts = ViuSnippetCatalog.Read(
            "Snippets", failures.Add, _ => EnumerateThenFail(),
            _ => new MemoryStream(Encoding.UTF8.GetBytes(CreateSnippet("prop"))));

        shortcuts.ShouldBeEmpty();
        failures.Count.ShouldBe(1);
        failures[0].ShouldContain("'Snippets'");
        failures[0].ShouldNotContain("first.snippet");
    }

    [Fact]
    public void Read_MissingFolder_ReturnsNoShortcutsAndLogsTheDirectory()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));
        var failures = new List<string>();

        ISet<string> shortcuts = ViuSnippetCatalog.Read(directory, failures.Add);

        shortcuts.ShouldBeEmpty();
        failures.Count.ShouldBe(1);
        failures[0].ShouldContain(directory);
    }

    [Fact]
    public void Read_EmptyFolder_ReturnsNoShortcutsWithoutAFailure()
    {
        var failures = new List<string>();

        ISet<string> shortcuts = ViuSnippetCatalog.Read(
            "Snippets", failures.Add, _ => [], _ => throw new InvalidOperationException());

        shortcuts.ShouldBeEmpty();
        failures.ShouldBeEmpty();
    }

    [Fact]
    public void Read_LoggingFails_ReturnsNoShortcutsWithoutThrowing()
    {
        ISet<string> shortcuts = ViuSnippetCatalog.Read(
            "Snippets", _ => throw new InvalidOperationException("Log unavailable."),
            _ => throw new DirectoryNotFoundException());

        shortcuts.ShouldBeEmpty();
    }

    [Fact]
    public void Read_NestedFolder_IncludesSnippetFilesAndIgnoresOtherFiles()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));
        var failures = new List<string>();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "Nested"));
            File.WriteAllText(Path.Combine(directory, "first.snippet"), CreateSnippet("first"));
            File.WriteAllText(Path.Combine(directory, "Nested", "second.snippet"), CreateSnippet("second"));
            File.WriteAllText(Path.Combine(directory, "ignored.txt"), "not XML");

            ISet<string> shortcuts = ViuSnippetCatalog.Read(directory, failures.Add);

            shortcuts.OrderBy(shortcut => shortcut, StringComparer.Ordinal).ShouldBe(["first", "second"]);
            failures.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ISet<string> ReadFiles(Dictionary<string, string> files, List<string> failures) =>
        ViuSnippetCatalog.Read("Snippets", failures.Add, _ => files.Keys,
            path => new MemoryStream(Encoding.UTF8.GetBytes(files[path])));

    private static IEnumerable<string> EnumerateThenFail()
    {
        yield return "first.snippet";
        throw new IOException("Directory became unavailable.");
    }

    private static string CreateSnippet(string shortcut) => $$"""
        <CodeSnippets xmlns="http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet">
          <CodeSnippet Format="1.0.0">
            <Header><Shortcut>{{shortcut}}</Shortcut></Header>
            <Snippet><Code Language="viu"><![CDATA[public int Value { get; set; }]]></Code></Snippet>
          </CodeSnippet>
        </CodeSnippets>
        """;
}
