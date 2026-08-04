using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Pins folding: a block-container fold covers the block content while the closing delimiter line
/// stays visible, and every multi-line element inside the template block adds a nested fold under the
/// same convention ([V01.01.12.07.07]). Ranges follow the Language Server Protocol folding-range
/// contract — the folded region runs from the end of <c>startLine</c> through the end of
/// <c>endLine</c>:
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_foldingRange">
/// Language Server Protocol 3.17 — textDocument/foldingRange</see>.
/// </summary>
public class FoldingRangeTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";

    private const string VueDocumentUri = "file:///workspace/Counter.vue";

    [Fact]
    public void GetFoldingRanges_BlockContent_KeepsClosingDelimiterVisible()
    {
        const string source =
            "<template>\n" +
            "    <div />\n" +
            "    <span />\n" +
            "</template>\n" +
            "@script {\n" +
            "    public int Count;\n" +
            "}\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 2));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 4, EndLine: 5));
    }

    [Fact]
    public void GetFoldingRanges_SingleLineBlock_ReturnsNoRange()
    {
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, "<template><div /></template>\n", 1);

        service.GetFoldingRanges(DocumentUri).ShouldBeEmpty();
    }

    // The section range and the element ranges coexist: the template's own fold is unchanged and each
    // nested multi-line element adds its own, parent before child, in document order.
    [Fact]
    public void GetFoldingRanges_NestedElements_ComposesSectionAndElementRanges()
    {
        const string source =
            "<template>\n" +                     // 0
            "    <article class=\"card\">\n" +   // 1
            "        <header>\n" +               // 2
            "            <h1>\n" +               // 3
            "                Title\n" +          // 4
            "            </h1>\n" +              // 5
            "        </header>\n" +              // 6
            "        <slot name=\"badge\" />\n" + // 7
            "    </article>\n" +                 // 8
            "</template>\n" +                    // 9
            "@script {\n" +                      // 10
            "    public int Count;\n" +          // 11
            "}\n";                               // 12
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(5);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 8));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 7));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 2, EndLine: 5));
        ranges[3].ShouldBe(new LanguageFoldingRange(StartLine: 3, EndLine: 4));
        ranges[4].ShouldBe(new LanguageFoldingRange(StartLine: 10, EndLine: 11));
    }

    [Fact]
    public void GetFoldingRanges_SiblingElements_FoldsEachSibling()
    {
        const string source =
            "<template>\n" +      // 0
            "    <div>\n" +       // 1
            "        one\n" +     // 2
            "    </div>\n" +      // 3
            "    <div>\n" +       // 4
            "        two\n" +     // 5
            "    </div>\n" +      // 6
            "</template>\n";      // 7
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(3);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 6));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 2));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 4, EndLine: 5));
    }

    // A single-line element folds nothing: there is no line between its open and close tags to hide.
    [Fact]
    public void GetFoldingRanges_SingleLineElement_ReturnsNoElementRange()
    {
        const string source =
            "<template>\n" +                        // 0
            "    <div><span>text</span></div>\n" +  // 1
            "</template>\n";                        // 2
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(1);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 1));
    }

    // A self-closing element folds nothing even when its open tag wraps across lines: it has no end
    // tag, so there is no closing delimiter for the fold to leave visible.
    [Fact]
    public void GetFoldingRanges_SelfClosingElement_ReturnsNoElementRange()
    {
        const string source =
            "<template>\n" +               // 0
            "    <slot\n" +                // 1
            "        name=\"badge\" />\n" + // 2
            "</template>\n";               // 3
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(1);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 2));
    }

    // A nested <template> slot fragment is an element like any other and folds like one.
    [Fact]
    public void GetFoldingRanges_NestedTemplateFragment_FoldsLikeAnyElement()
    {
        const string source =
            "<template>\n" +                     // 0
            "    <MyCard>\n" +                   // 1
            "        <template #badge>\n" +      // 2
            "            <span>New</span>\n" +   // 3
            "        </template>\n" +            // 4
            "    </MyCard>\n" +                  // 5
            "</template>\n";                     // 6
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(3);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 5));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 4));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 2, EndLine: 3));
    }

    // The legacy @template container ([V01.01.06.10]) holds the same markup, and its content starts on
    // the line after the header — the element lines are still the document's own lines.
    [Fact]
    public void GetFoldingRanges_LegacyTemplateContainer_FoldsElements()
    {
        const string source =
            "@template {\n" +     // 0
            "    <div>\n" +       // 1
            "        text\n" +    // 2
            "    </div>\n" +      // 3
            "}\n";                // 4
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 3));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 2));
    }

    // The .vue compatibility container ([V01.01.06.09]) projects into the same block hierarchy, so its
    // template elements fold identically.
    [Fact]
    public void GetFoldingRanges_VueContainer_FoldsElements()
    {
        const string source =
            "<template>\n" +                  // 0
            "    <div class=\"card\">\n" +    // 1
            "        <p>Hello</p>\n" +        // 2
            "    </div>\n" +                  // 3
            "</template>\n";                  // 4
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(VueDocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 3));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 2));
    }

    // An element the parse recovered rather than matched gets no range: its recovered span ends
    // wherever recovery landed, which is not a place a fold may end. The document still folds
    // everything that is well formed, and nothing throws.
    [Fact]
    public void GetFoldingRanges_UnclosedElement_ReturnsNoRangeForTheUnclosedElement()
    {
        const string source =
            "<template>\n" +      // 0
            "    <div>\n" +       // 1
            "        text\n" +    // 2
            "</template>\n";      // 3
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(1);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 2));
    }

    // The element closed by an ancestor's end tag is the recovered one; the ancestor that does carry
    // its own end tag still folds, and no range inverts.
    [Fact]
    public void GetFoldingRanges_ElementClosedByAncestorEndTag_FoldsOnlyTheMatchedAncestor()
    {
        const string source =
            "<template>\n" +                 // 0
            "    <article>\n" +              // 1
            "        <header>\n" +           // 2
            "            <h1>Title</h1>\n" + // 3
            "    </article>\n" +             // 4
            "</template>\n";                 // 5
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 4));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 3));
        foreach (var range in ranges)
        {
            range.EndLine.ShouldBeGreaterThan(range.StartLine);
        }
    }

    // A void element is not a container: it opens no fold, and the sibling that follows it folds on
    // its own lines rather than on a span that swallowed the void element's line.
    [Fact]
    public void GetFoldingRanges_VoidElement_ReturnsNoRangeAndLeavesSiblingsIntact()
    {
        const string source =
            "<template>\n" +        // 0
            "    <div>\n" +         // 1
            "        one<br>\n" +   // 2
            "        two\n" +       // 3
            "    </div>\n" +        // 4
            "</template>\n";        // 5
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 4));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 3));
    }

    // The document snapshot caches the element ranges, so repeated requests answer identically; an
    // edit replaces the snapshot and the ranges move with the edited text.
    [Fact]
    public void GetFoldingRanges_AfterEdit_RecomputesAgainstTheEditedDocument()
    {
        const string source =
            "<template>\n" +      // 0
            "    <div>\n" +       // 1
            "        text\n" +    // 2
            "    </div>\n" +      // 3
            "</template>\n";      // 4
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var first = service.GetFoldingRanges(DocumentUri);
        var second = service.GetFoldingRanges(DocumentUri);
        service.ChangeDocument(
            DocumentUri,
            2,
            [new LanguageDocumentChange(null, "<template>\n    <div>\n        one\n        two\n    </div>\n</template>\n")]);
        var third = service.GetFoldingRanges(DocumentUri);

        second.ShouldBe(first);
        first.Count.ShouldBe(2);
        first[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 2));
        third.Count.ShouldBe(2);
        third[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 3));
    }
}
