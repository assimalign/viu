using System;
using System.Collections.Generic;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Computes the element-level folding ranges inside a template block ([V01.01.12.07.07]): one range
/// per multi-line element that carries its own end tag, in document order, addressed in the
/// document's zero-based line coordinates so the ranges compose with the section ranges the service
/// emits for the block containers themselves.
/// </summary>
/// <remarks>
/// <para>
/// The container parse slices a template block but never parses its markup, so the elements come from
/// a template parse of the block content. That parse is per document, not per request: the service
/// caches the computed ranges on the immutable open-document snapshot, so an unedited document answers
/// every folding request from the first parse.
/// </para>
/// <para>
/// The markup is parsed with the DOM host's options so void elements (<c>&lt;br&gt;</c>,
/// <c>&lt;img&gt;</c>) and raw-text elements behave as they do in a browser: a void element is not a
/// container and therefore never opens a fold that would otherwise swallow its following siblings.
/// </para>
/// <para>
/// Ranges follow the Language Server Protocol folding-range contract — the folded region runs from the
/// end of <c>startLine</c> through the end of <c>endLine</c>, so an element's fold stops one line above
/// its end tag and the end tag stays visible while the element is collapsed, matching the convention
/// the block-container ranges already use.
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_foldingRange">
/// Language Server Protocol 3.17 — textDocument/foldingRange</see>.
/// </para>
/// <para>
/// Keeping the end tag visible is the <em>markup</em> convention and is deliberately not the one script
/// blocks use: C# constructs collapse with both delimiters hidden, beside their signature, the way the
/// C# editor collapses a <c>.cs</c> file (see <see cref="ScriptFoldingRangeCollector"/>,
/// [V01.01.12.07.10]). The split is per family and intentional — an author reads a collapsed element by
/// its tags and a collapsed method by its signature.
/// </para>
/// </remarks>
internal static class TemplateFoldingRangeCollector
{
    /// <summary>
    /// Collects the folding ranges for the multi-line elements inside <paramref name="template"/>.
    /// Returns an empty list when there is no template block, when its content is empty, and for a
    /// block whose elements all fold to nothing.
    /// </summary>
    /// <param name="template">The container parse's template block, or <see langword="null"/>.</param>
    /// <returns>The element ranges in document order.</returns>
    internal static IReadOnlyList<LanguageFoldingRange> Collect(
        SingleFileComponentTemplateBlock? template)
    {
        if (template is null || template.Content.Length == 0)
        {
            return Array.Empty<LanguageFoldingRange>();
        }

        // Elements the parser recovered rather than matched: an unclosed element is reported at the
        // offset of its open tag, and gets no range at all — its recovered span ends wherever the
        // recovery landed, which is not a place a fold may end.
        var unclosedOffsets = new HashSet<int>();
        var options = ParserOptions.CreateHtml();
        options.OnError = error =>
        {
            if (error.Code == CompilerErrorCode.XMissingEndTag)
            {
                unclosedOffsets.Add(error.Location.Start.Offset);
            }
        };

        // Recoverable by contract: malformed markup is reported through OnError and never throws.
        var root = TemplateParser.Parse(template.Content, options);
        // Content line 0 sits on the document line the content starts on — the block's own open-tag
        // line for a tag container, the line after the header for an @-block container.
        var lineMap = ContentLineMap.Create(template);

        var ranges = new List<LanguageFoldingRange>();
        var pending = new Stack<ElementNode>();
        PushElements(root.Children, pending);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            var range = CreateRange(
                element,
                template.Content.Length,
                lineMap,
                unclosedOffsets);
            if (range is not null)
            {
                ranges.Add(range);
            }

            PushElements(element.Children, pending);
        }

        return ranges;
    }

    // Depth-first in document order: children are pushed in reverse so the first child pops first,
    // which puts a parent's range ahead of its descendants' and siblings' in source order.
    private static void PushElements(
        SyntaxList<TemplateChildNode> children,
        Stack<ElementNode> pending)
    {
        for (var index = children.Count - 1; index >= 0; index--)
        {
            if (children[index] is ElementNode element)
            {
                pending.Push(element);
            }
        }
    }

    private static LanguageFoldingRange? CreateRange(
        ElementNode element,
        int contentLength,
        ContentLineMap lineMap,
        HashSet<int> unclosedOffsets)
    {
        // A self-closing element has no end tag to keep visible, and neither does an element the
        // parser had to recover; both fold to nothing rather than to a span the author never wrote.
        if (element.IsSelfClosing ||
            unclosedOffsets.Contains(element.Location.Start.Offset) ||
            !EndsWithMatchingCloseTag(element.Location.Source, element.Tag))
        {
            return null;
        }

        var startOffset = element.Location.Start.Offset;
        // The span end is exclusive, so the last character of the end tag is the offset before it —
        // asking for the end tag's own line rather than for whatever follows the '>'.
        var closeTagOffset = element.Location.End.Offset - 1;
        if (startOffset < 0 || closeTagOffset <= startOffset || closeTagOffset >= contentLength)
        {
            return null;
        }

        var startLine = lineMap.GetDocumentLine(startOffset);
        var endLine = lineMap.GetDocumentLine(closeTagOffset) - 1;
        // A single-line element, and an element whose end tag opens the line right after its open
        // tag, fold nothing — and a range can never invert, whatever span the parse produced.
        return endLine > startLine
            ? new LanguageFoldingRange(startLine, endLine)
            : null;
    }

    // Whether the element's own source ends with its end tag. This is what separates a matched
    // element from a void element, a self-closing element, and an element the parse closed
    // implicitly at an ancestor's end tag: only a matched element's span ends with '</tag>'.
    private static bool EndsWithMatchingCloseTag(string source, string tag)
    {
        var index = source.Length - 1;
        if (index < 0 || source[index] != '>')
        {
            return false;
        }

        index--;
        while (index >= 0 && char.IsWhiteSpace(source[index]))
        {
            index--;
        }

        var nameStart = index + 1 - tag.Length;
        // '</' has to precede the name, so the name cannot start before offset 2.
        if (nameStart < 2 ||
            string.Compare(source, nameStart, tag, 0, tag.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        return source[nameStart - 1] == '/' && source[nameStart - 2] == '<';
    }
}
