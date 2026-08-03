using System;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Pins the <c>@script</c> member children of the document outline ([V01.01.06.11]): each declared
/// field, property, and method surfaces as a child of the script block symbol, with document
/// coordinates composed through the same block-to-file arithmetic the emitted <c>#line</c> map uses
/// (<c>SingleFileComponentDiagnostics.ComposeToFilePosition</c>), so outline positions and build
/// positions agree by construction.
/// </summary>
public class DocumentSymbolMemberChildrenTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";

    private const string Source =
        "<template>\n" +
        "    <div>{{ Count }}</div>\n" +
        "</template>\n" +
        "@script {\n" +
        "    public int Count;\n" +
        "    public string Title { get; set; } = \"Viu\";\n" +
        "    public void Increment() => Count++;\n" +
        "}\n";

    [Fact]
    public void GetDocumentSymbols_ScriptMembers_SurfaceAsChildrenInDeclarationOrder()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, Source, 1);

        var script = service.GetDocumentSymbols(DocumentUri)
            .Single(symbol => symbol.Name == "@script");

        script.Children.Select(child => child.Name)
            .ShouldBe(["Count", "Title", "Increment"]);
        script.Children.Select(child => child.Kind)
            .ShouldBe(
            [
                LanguageSymbolKind.Field,
                LanguageSymbolKind.Property,
                LanguageSymbolKind.Method,
            ]);
        script.Children[0].Detail.ShouldBe("int");
        script.Children[1].Detail.ShouldBe("string");
    }

    [Fact]
    public void GetDocumentSymbols_ScriptMemberSelections_SliceTheDeclaredIdentifiers()
    {
        // The strongest possible pin on the position composition: the selection range, applied to the
        // real document text, must reproduce the declared identifier exactly. Any off-by-one in the
        // block-to-file arithmetic fails this on every member.
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, Source, 1);

        var script = service.GetDocumentSymbols(DocumentUri)
            .Single(symbol => symbol.Name == "@script");

        foreach (var child in script.Children)
        {
            Slice(Source, child.SelectionRange).ShouldBe(child.Name);
            child.SelectionRange.Start.Line.ShouldBe(child.Range.Start.Line);
            child.Range.Start.Line.ShouldBeGreaterThan(script.Range.Start.Line);
            child.Range.End.Line.ShouldBeLessThan(script.Range.End.Line);
        }

        // The field's member range starts at its declared line: line index 4 carries "public int Count;".
        script.Children[0].Range.Start.Line.ShouldBe(4);
    }

    [Fact]
    public void GetDocumentSymbols_EmptyScript_HasNoChildren()
    {
        const string source =
            "<template>\n" +
            "    <div />\n" +
            "</template>\n" +
            "@script {\n" +
            "}\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var script = service.GetDocumentSymbols(DocumentUri)
            .Single(symbol => symbol.Name == "@script");

        script.Children.ShouldBeEmpty();
    }

    private static string Slice(string text, LanguageRange range)
    {
        var lines = text.Split('\n');
        range.End.Line.ShouldBe(range.Start.Line, "identifier selections never span lines");
        return lines[range.Start.Line].Substring(
            range.Start.Character,
            range.End.Character - range.Start.Character);
    }
}
