using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins folding, and the two collapse conventions it deliberately runs. A block-container fold covers
/// the block content while the closing delimiter line stays visible, and every multi-line element inside
/// the template block adds a nested fold under the same markup convention ([V01.01.12.07.07]). A
/// multi-line C# construct inside a script block instead collapses the way the C# editor collapses a
/// <c>.cs</c> file ([V01.01.12.07.10]): the range starts on the line of the token before the opening
/// delimiter, so the collapse badge sits beside the signature, and ends <em>on</em> the closing
/// delimiter's line, so both braces are hidden. Ranges follow the Language Server Protocol folding-range
/// contract — the folded region runs from the end of <c>startLine</c> through the end of <c>endLine</c>:
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

    // [V01.01.12.07.10] A method collapses beside its signature with both braces hidden, as it does in a
    // .cs file: the range starts on the signature line (the ')' precedes the brace) and ends on the
    // closing brace's line. The section range around it still keeps the block's own '}' visible.
    [Fact]
    public void GetFoldingRanges_ScriptMethodBody_CollapsesBesideTheSignature()
    {
        const string source =
            "<template>\n" +                     // 0
            "    <div />\n" +                    // 1
            "</template>\n" +                    // 2
            "@script {\n" +                      // 3
            "    public int Count;\n" +          // 4
            "\n" +                               // 5
            "    public void Increment()\n" +    // 6
            "    {\n" +                          // 7
            "        Count++;\n" +               // 8
            "    }\n" +                          // 9
            "}\n";                               // 10
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(3);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 1));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 3, EndLine: 9));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 6, EndLine: 9));
    }

    // An expression-bodied member folds nothing however far its '=>' body wraps: it declares no
    // delimiter pair, and the C# editor does not collapse one either. The block-bodied member beside it
    // still collapses from its signature line.
    [Fact]
    public void GetFoldingRanges_ScriptExpressionBodiedMember_ReturnsNoRange()
    {
        const string source =
            "@script {\n" +                          // 0
            "    public int Total =>\n" +            // 1
            "        Count +\n" +                    // 2
            "        Offset;\n" +                    // 3
            "\n" +                                   // 4
            "    public int Doubled()\n" +           // 5
            "    {\n" +                              // 6
            "        return Total * 2;\n" +          // 7
            "    }\n" +                              // 8
            "}\n";                                   // 9
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 8));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 5, EndLine: 8));
    }

    // The accessor list and each accessor's own block are separate delimiter pairs, so both fold and
    // they nest — the list anchored on the property identifier, the getter on its 'get' keyword. The
    // expression-bodied 'set' contributes nothing.
    [Fact]
    public void GetFoldingRanges_ScriptAccessorList_FoldsListAndAccessorBodies()
    {
        const string source =
            "@script {\n" +                             // 0
            "    public int Count\n" +                  // 1
            "    {\n" +                                 // 2
            "        get\n" +                           // 3
            "        {\n" +                             // 4
            "            return count;\n" +             // 5
            "        }\n" +                             // 6
            "        set => count = value;\n" +         // 7
            "    }\n" +                                 // 8
            "}\n";                                      // 9
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(3);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 8));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 8));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 3, EndLine: 6));
    }

    // The showcase's declaration shape: a collection expression spanning lines collapses onto the
    // declaration line, anchored on the '=' that precedes its opening bracket, with both brackets hidden.
    [Fact]
    public void GetFoldingRanges_ScriptCollectionExpression_CollapsesOntoTheDeclaration()
    {
        const string source =
            "@script {\n" +                                  // 0
            "    private readonly string[] parameters =\n" + // 1
            "    [\n" +                                      // 2
            "        \"first\",\n" +                         // 3
            "        \"second\",\n" +                        // 4
            "    ];\n" +                                     // 5
            "}\n";                                           // 6
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 5));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 5));
    }

    // A multi-line object initializer collapses onto the declaration line it initializes, anchored on
    // the ')' of the object creation that precedes its opening brace.
    [Fact]
    public void GetFoldingRanges_ScriptObjectInitializer_CollapsesOntoTheDeclaration()
    {
        const string source =
            "@script {\n" +                                     // 0
            "    private readonly Options options = new()\n" +  // 1
            "    {\n" +                                         // 2
            "        Name = \"card\",\n" +                      // 3
            "    };\n" +                                        // 4
            "}\n";                                              // 5
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 4));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 4));
    }

    // A lambda's block body is a block like any other and folds on the same rule, anchored on the '=>'
    // that precedes its opening brace.
    [Fact]
    public void GetFoldingRanges_ScriptLambdaBlockBody_Folds()
    {
        const string source =
            "@script {\n" +                                    // 0
            "    private readonly Action handler = () =>\n" +  // 1
            "    {\n" +                                        // 2
            "        Count++;\n" +                             // 3
            "    };\n" +                                       // 4
            "}\n";                                             // 5
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 4));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 4));
    }

    // A local function nested in a method body collapses beside its own signature, and the two ranges
    // compose: the enclosing method's range comes first and contains the local function's.
    [Fact]
    public void GetFoldingRanges_ScriptNestedLocalFunction_ComposesWithTheMethodBody()
    {
        const string source =
            "@script {\n" +                    // 0
            "    public void Run()\n" +        // 1
            "    {\n" +                        // 2
            "        void Inner()\n" +         // 3
            "        {\n" +                    // 4
            "            Count++;\n" +         // 5
            "        }\n" +                    // 6
            "\n" +                             // 7
            "        Inner();\n" +             // 8
            "    }\n" +                        // 9
            "}\n";                             // 10
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(3);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 9));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 9));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 3, EndLine: 6));
    }

    // A type declared inside the script collapses beside its declaration, anchored on the type name.
    [Fact]
    public void GetFoldingRanges_ScriptNestedTypeDeclaration_Folds()
    {
        const string source =
            "@script {\n" +                          // 0
            "    private sealed class State\n" +     // 1
            "    {\n" +                              // 2
            "        public int Count;\n" +          // 3
            "    }\n" +                              // 4
            "}\n";                                   // 5
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 4));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 4));
    }

    // A matched #region/#endregion pair collapses from the #region line through and INCLUDING the
    // #endregion line, which is what the C# editor does with a region.
    [Fact]
    public void GetFoldingRanges_ScriptRegionDirectives_CollapseThroughTheEndRegion()
    {
        const string source =
            "@script {\n" +                     // 0
            "    #region State\n" +             // 1
            "    private int count;\n" +        // 2
            "    #endregion\n" +                // 3
            "}\n";                              // 4
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 3));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 3));
    }

    // A single-line member folds nothing: its anchor, its braces, and its body share one line, so the
    // computed range would not span a line at all.
    [Fact]
    public void GetFoldingRanges_ScriptSingleLineMethod_ReturnsNoRange()
    {
        const string source =
            "@script {\n" +                                    // 0
            "    public void Increment() { Count++; }\n" +     // 1
            "}\n";                                             // 2
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(1);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 1));
    }

    // A member left unclosed at the end of the block gets no range: C# error recovery hands its closing
    // brace to the analyzer's own synthetic wrapper, which is not a place a fold may end. The well-formed
    // sibling above it is unaffected, and nothing throws or inverts.
    [Fact]
    public void GetFoldingRanges_ScriptUnclosedMethodBody_ReturnsNoRangeForItAndLeavesSiblingsIntact()
    {
        const string source =
            "@script {\n" +                    // 0
            "    public void First()\n" +      // 1
            "    {\n" +                        // 2
            "        Count++;\n" +             // 3
            "    }\n" +                        // 4
            "\n" +                             // 5
            "    public void Second()\n" +     // 6
            "    {\n" +                        // 7
            "        Count++;\n" +             // 8
            "}\n";                             // 9
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 8));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 4));
        foreach (var range in ranges)
        {
            range.EndLine.ShouldBeGreaterThan(range.StartLine);
        }
    }

    // The leading using run is hoisted out of the member region before the C# is parsed, so the cut has
    // to be re-added when a construct's offsets are mapped back onto the document.
    [Fact]
    public void GetFoldingRanges_ScriptWithLeadingUsings_MapsPastTheHoistedUsingRegion()
    {
        const string source =
            "@script {\n" +                    // 0
            "    using System;\n" +            // 1
            "\n" +                             // 2
            "    public void Run()\n" +        // 3
            "    {\n" +                        // 4
            "        Count++;\n" +             // 5
            "    }\n" +                        // 6
            "}\n";                             // 7
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 6));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 3, EndLine: 6));
    }

    // The .vue single-file-component container ([V01.01.06.09]) is a declared external compatibility
    // target: its <script> block holds the same C# and folds under the same rule as the .viu @script
    // block, and the block's content starting on the open-tag line does not shift the construct lines.
    [Fact]
    public void GetFoldingRanges_VueScriptBlock_FoldsScriptConstructs()
    {
        const string source =
            "<template>\n" +                   // 0
            "    <div />\n" +                  // 1
            "</template>\n" +                  // 2
            "<script>\n" +                     // 3
            "public void Increment()\n" +      // 4
            "{\n" +                            // 5
            "    Count++;\n" +                 // 6
            "}\n" +                            // 7
            "</script>\n";                     // 8
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(VueDocumentUri);

        ranges.Count.ShouldBe(3);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 1));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 3, EndLine: 7));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 4, EndLine: 7));
    }

    // A .vue author may close the last construct on the same line as </script>. The construct's
    // inclusive end is clamped to its section's end so it still nests inside the section instead of
    // outliving it.
    [Fact]
    public void GetFoldingRanges_VueConstructClosingOnTheEndTagLine_StaysInsideItsSection()
    {
        const string source =
            "<script>\n" +                     // 0
            "public void Increment()\n" +      // 1
            "{\n" +                            // 2
            "    Count++;\n" +                 // 3
            "}</script>\n";                    // 4
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(VueDocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 3));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 3));
        ranges[1].EndLine.ShouldBeLessThanOrEqualTo(ranges[0].EndLine);
    }

    // The .vue container's second script block — <script setup> ([V01.01.06.09]) — is a script block
    // like any other and folds its interior the same way.
    [Fact]
    public void GetFoldingRanges_VueScriptSetupBlock_FoldsScriptConstructs()
    {
        const string source =
            "<script setup>\n" +               // 0
            "public void Increment()\n" +      // 1
            "{\n" +                            // 2
            "    Count++;\n" +                 // 3
            "}\n" +                            // 4
            "</script>\n";                     // 5
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(VueDocumentUri);

        ranges.Count.ShouldBe(2);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 4));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 4));
    }

    // All three folding families in one document, each under its own convention: the block sections and
    // the nested template element keep their closing delimiters visible, the script construct hides its
    // braces and collapses beside the signature. Each is emitted right after the section it belongs to,
    // so the whole result stays in document order, and the construct still closes strictly inside its
    // section rather than crossing it.
    [Fact]
    public void GetFoldingRanges_SectionElementAndScriptConstruct_ComposeInDocumentOrder()
    {
        const string source =
            "<template>\n" +                     // 0
            "    <div class=\"card\">\n" +       // 1
            "        <span>Hi</span>\n" +        // 2
            "    </div>\n" +                     // 3
            "</template>\n" +                    // 4
            "@script {\n" +                      // 5
            "    public void Increment()\n" +    // 6
            "    {\n" +                          // 7
            "        Count++;\n" +               // 8
            "    }\n" +                          // 9
            "}\n";                               // 10
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var ranges = service.GetFoldingRanges(DocumentUri);

        ranges.Count.ShouldBe(4);
        ranges[0].ShouldBe(new LanguageFoldingRange(StartLine: 0, EndLine: 3));
        ranges[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 2));
        ranges[2].ShouldBe(new LanguageFoldingRange(StartLine: 5, EndLine: 9));
        ranges[3].ShouldBe(new LanguageFoldingRange(StartLine: 6, EndLine: 9));
    }

    // The document snapshot caches the script ranges too, so repeated requests answer identically; an
    // edit replaces the snapshot and the range grows with the body it collapses.
    [Fact]
    public void GetFoldingRanges_ScriptAfterEdit_RecomputesAgainstTheEditedDocument()
    {
        const string source =
            "@script {\n" +                    // 0
            "    public void Run()\n" +        // 1
            "    {\n" +                        // 2
            "        Count++;\n" +             // 3
            "    }\n" +                        // 4
            "}\n";                             // 5
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var first = service.GetFoldingRanges(DocumentUri);
        var second = service.GetFoldingRanges(DocumentUri);
        service.ChangeDocument(
            DocumentUri,
            2,
            [new LanguageDocumentChange(
                null,
                "@script {\n    public void Run()\n    {\n        Count++;\n        Count++;\n    }\n}\n")]);
        var third = service.GetFoldingRanges(DocumentUri);

        second.ShouldBe(first);
        first.Count.ShouldBe(2);
        first[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 4));
        third.Count.ShouldBe(2);
        third[1].ShouldBe(new LanguageFoldingRange(StartLine: 1, EndLine: 5));
    }
}
