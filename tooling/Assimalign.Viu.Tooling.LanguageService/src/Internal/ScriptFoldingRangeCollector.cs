using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Tooling.SingleFileComponent;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Computes the construct-level folding ranges inside a script block ([V01.01.12.07.10]): one range per
/// multi-line delimited region the block's C# declares — member and statement blocks, accessor lists,
/// object and collection initializers, collection expressions, nested type bodies, switch bodies, and
/// <c>#region</c> pairs — in document order, addressed in the document's zero-based line coordinates so
/// the ranges compose with the section ranges and the template-element ranges the service already emits.
/// A construct collapses beside its signature with both delimiters hidden, the way the C# editor
/// collapses a <c>.cs</c> file; see the convention split below.
/// </summary>
/// <remarks>
/// <para>
/// The constructs come from the shared projection core's probe parse
/// (<see cref="ScriptBlockAnalyzer.ParseProbe"/>) — the same synthetic partial-class wrapper and
/// leading-using split the build's source generator and the editor's member description use — so the
/// editor folds exactly the constructs the build sees. That parse is per document, not per request: the
/// service caches the computed ranges on the immutable open-document snapshot, so an unedited document
/// answers every folding request from the first parse.
/// </para>
/// <para>
/// <b>The C# collapse convention, and why it is not the markup one.</b> Viu deliberately runs
/// <em>two</em> folding conventions, one per family, because an author reads a collapsed script block
/// as C# and a collapsed template as markup ([V01.01.12.07.10], from the requirement that a script
/// block collapse the way the C# editor collapses a <c>.cs</c> file):
/// <list type="bullet">
/// <item>
/// <b>Markup keeps its closer visible.</b> A template element's fold ends one line <em>above</em> its
/// end tag, so <c>&lt;/div&gt;</c> still marks where the collapsed element stops
/// ([V01.01.12.07.07]) — the block-container section ranges do the same for <c>}</c> and
/// <c>&lt;/template&gt;</c>.
/// </item>
/// <item>
/// <b>C# swallows its closer.</b> A script construct's fold ends <em>on</em> the line holding its
/// closing <c>}</c> or <c>]</c>, so the collapsed method reads
/// <c>public void Track(string message) […]</c> with both braces hidden, exactly as a collapsed
/// method reads in a <c>.cs</c> file.
/// </item>
/// </list>
/// The start line follows from the same requirement. Under the Language Server Protocol folding-range
/// contract the folded region runs from the <em>end</em> of <c>startLine</c> through the end of
/// <c>endLine</c>, so whatever line a range starts on is the line the collapse badge sits beside. A
/// range therefore starts on the line of the last token <em>before</em> the opening delimiter — the
/// <c>)</c> closing a signature, an accessor keyword, a declared identifier, an <c>=</c> — which puts
/// the badge beside the signature instead of below it under this repository's brace-on-its-own-line
/// style. When the delimiter already shares that token's line (<c>void Method() {</c>) the two resolve
/// to one line and nothing shifts. A <c>#region</c> pair follows the C# editor too: it collapses from
/// the <c>#region</c> line through and including the <c>#endregion</c> line.
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_foldingRange">
/// Language Server Protocol 3.17 — textDocument/foldingRange</see>.
/// </para>
/// <para>
/// The two conventions still compose. A section's fold ends one line above the block's own closing
/// delimiter, and a construct's inclusive end is clamped to that line, so a construct always nests
/// inside its section rather than crossing it — including the <c>.vue</c> case where the last construct
/// closes on the same line as <c>&lt;/script&gt;</c>.
/// </para>
/// <para>
/// An expression-bodied member folds nothing however far its <c>=&gt;</c> body wraps: it declares no
/// delimiter pair, and the C# editor does not collapse one either. Parenthesized argument and parameter
/// lists are deliberately not folded — a collapsed call would swallow the arguments an author reads the
/// call by.
/// </para>
/// </remarks>
internal static class ScriptFoldingRangeCollector
{
    /// <summary>
    /// Collects the folding ranges for the multi-line C# constructs inside <paramref name="script"/>.
    /// Returns an empty list when there is no script block, when its content declares no members, and
    /// for a block whose constructs all fold to nothing.
    /// </summary>
    /// <param name="script">The container parse's script block, or <see langword="null"/>.</param>
    /// <returns>The construct ranges in document order, outermost first.</returns>
    internal static IReadOnlyList<LanguageFoldingRange> Collect(
        SingleFileComponentScriptBlock? script)
    {
        if (script is null || script.Content.Length == 0)
        {
            return Array.Empty<LanguageFoldingRange>();
        }

        // Recoverable by contract: the probe parse ignores diagnostics, so malformed C# still yields the
        // well-formed constructs and never throws.
        var parse = ScriptBlockAnalyzer.ParseProbe(script.Content);
        if (parse.Probe is null)
        {
            return Array.Empty<LanguageFoldingRange>();
        }

        var spans = new List<(int Start, int Close)>();
        CollectDelimitedSpans(parse, spans);
        CollectRegionSpans(parse, spans);
        if (spans.Count == 0)
        {
            return Array.Empty<LanguageFoldingRange>();
        }

        // Document order, outermost first: the region directives are collected separately from the
        // syntax nodes, so one sort over content offsets is what puts every family in source order.
        spans.Sort(static (left, right) => left.Start != right.Start
            ? left.Start.CompareTo(right.Start)
            : right.Close.CompareTo(left.Close));

        var lineMap = ContentLineMap.Create(script);
        // The line the block's own section range ends on, computed exactly as the service computes it.
        // An inclusive construct end can reach it but must never pass it: a .vue author may close the
        // last construct on the same line as </script>, and a construct that outlived its section would
        // cross the section range instead of nesting inside it.
        var sectionEndLine = Math.Max(script.Location.End.Line - 1, 0) - 1;
        var ranges = new List<LanguageFoldingRange>(spans.Count);
        // Two constructs can share one span's lines (an initializer opened and closed on the same lines
        // as the block containing it); the editor gets one range for those lines, not two.
        var emitted = new HashSet<LanguageFoldingRange>();
        foreach (var (start, close) in spans)
        {
            var startLine = lineMap.GetDocumentLine(start);
            // Inclusive: the closing delimiter's own line is folded away, so the collapsed construct
            // hides both braces the way the C# editor does.
            var endLine = Math.Min(lineMap.GetDocumentLine(close), sectionEndLine);
            // A single-line construct folds nothing — and a range can never invert, whatever span the
            // parse produced.
            if (endLine <= startLine)
            {
                continue;
            }

            var range = new LanguageFoldingRange(startLine, endLine);
            if (emitted.Add(range))
            {
                ranges.Add(range);
            }
        }

        return ranges;
    }

    // Every delimited region the script's C# declares. The walk is over the probe's descendants only —
    // the probe class's own braces are the synthetic wrapper's, never the author's.
    private static void CollectDelimitedSpans(
        ScriptProbeParse parse,
        List<(int Start, int Close)> spans)
    {
        foreach (var node in parse.Probe!.DescendantNodes())
        {
            switch (node)
            {
                // Method, constructor, local-function, accessor, and lambda bodies are all one kind:
                // a brace-delimited statement block. Nested statement blocks fold on the same rule.
                case BlockSyntax block:
                    AddSpan(parse, spans, block.OpenBraceToken, block.CloseBraceToken);
                    break;
                case AccessorListSyntax accessors:
                    AddSpan(parse, spans, accessors.OpenBraceToken, accessors.CloseBraceToken);
                    break;
                // Object, collection, array, and `with` initializers.
                case InitializerExpressionSyntax initializer:
                    AddSpan(parse, spans, initializer.OpenBraceToken, initializer.CloseBraceToken);
                    break;
                case AnonymousObjectCreationExpressionSyntax anonymousObject:
                    AddSpan(parse, spans, anonymousObject.OpenBraceToken, anonymousObject.CloseBraceToken);
                    break;
                // The bracket-delimited collection expression (`Parameters = [ ... ];`).
                case CollectionExpressionSyntax collection:
                    AddSpan(parse, spans, collection.OpenBracketToken, collection.CloseBracketToken);
                    break;
                // A type declared inside the script: class, struct, interface, record, enum.
                case TypeDeclarationSyntax type:
                    AddSpan(parse, spans, type.OpenBraceToken, type.CloseBraceToken);
                    break;
                case EnumDeclarationSyntax enumeration:
                    AddSpan(parse, spans, enumeration.OpenBraceToken, enumeration.CloseBraceToken);
                    break;
                case SwitchStatementSyntax switchStatement:
                    AddSpan(parse, spans, switchStatement.OpenBraceToken, switchStatement.CloseBraceToken);
                    break;
                case SwitchExpressionSyntax switchExpression:
                    AddSpan(parse, spans, switchExpression.OpenBraceToken, switchExpression.CloseBraceToken);
                    break;
            }
        }
    }

    // The #region/#endregion pairs, matched innermost-first over the directives in source order. The
    // directive lines are their own delimiters, so no signature anchor applies: the fold runs from the
    // #region line through the #endregion line, which the inclusive end line already covers. An
    // unmatched directive — a #region the author has not closed yet, or an #endregion whose #region sits
    // in the hoisted using region the probe never sees — contributes nothing.
    private static void CollectRegionSpans(
        ScriptProbeParse parse,
        List<(int Start, int Close)> spans)
    {
        var pending = new Stack<int>();
        foreach (var trivia in parse.Probe!.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
            {
                pending.Push(trivia.SpanStart);
            }
            else if (trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia) && pending.Count > 0)
            {
                AddSpan(parse, spans, pending.Pop(), trivia.SpanStart);
            }
        }
    }

    // A delimiter pair contributes a span only when BOTH delimiters are the author's own text. Roslyn
    // recovery routinely hands an unclosed construct the probe wrapper's closing brace, or synthesizes a
    // missing one, and neither is a place a fold may end.
    private static void AddSpan(
        ScriptProbeParse parse,
        List<(int Start, int Close)> spans,
        SyntaxToken open,
        SyntaxToken close)
    {
        if (open.IsMissing || close.IsMissing || open.Span.IsEmpty || close.Span.IsEmpty)
        {
            return;
        }

        // The fold is anchored on the token BEFORE the opening delimiter — one rule covering the ')' of
        // a signature or a switch's governing parentheses, an accessor keyword, a declared identifier or
        // base list end, the '=' or '=>' ahead of an initializer, and the 'new' of an anonymous object —
        // so a brace written on its own line does not push the collapse badge below the signature. The
        // anchor is used only when it is the author's own text: the probe wrapper's own brace precedes
        // the first construct in the block, and it is not a line the author can see.
        var startOffset = open.SpanStart;
        var anchor = open.GetPreviousToken();
        if (!anchor.IsMissing &&
            !anchor.Span.IsEmpty &&
            anchor.SpanStart < startOffset &&
            parse.TryGetContentOffset(anchor.SpanStart, out _))
        {
            startOffset = anchor.SpanStart;
        }

        AddSpan(parse, spans, startOffset, close.SpanStart);
    }

    private static void AddSpan(
        ScriptProbeParse parse,
        List<(int Start, int Close)> spans,
        int openProbeOffset,
        int closeProbeOffset)
    {
        if (parse.TryGetContentOffset(openProbeOffset, out var start) &&
            parse.TryGetContentOffset(closeProbeOffset, out var close) &&
            close > start)
        {
            spans.Add((start, close));
        }
    }
}
