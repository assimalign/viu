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
/// A range spans a <em>delimiter pair</em>: it starts on the line holding the opening <c>{</c> or
/// <c>[</c> (the <c>#region</c> line for a region) and ends one line above the line holding the closing
/// delimiter. Under the Language Server Protocol folding-range contract the folded region runs from the
/// end of <c>startLine</c> through the end of <c>endLine</c>, so stopping one line short keeps the
/// closing delimiter visible while the construct is collapsed — the convention the block-container and
/// template-element ranges already use, which is what lets all three families compose.
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_foldingRange">
/// Language Server Protocol 3.17 — textDocument/foldingRange</see>.
/// </para>
/// <para>
/// An expression-bodied member folds nothing however far its <c>=&gt;</c> body wraps: it carries no
/// closing delimiter for a fold to leave visible, the same reason a self-closing element folds nothing
/// ([V01.01.12.07.07]). Parenthesized argument and parameter lists are deliberately not folded for the
/// same reason a fold there reads badly — the collapsed call would leave a bare <c>)</c> line.
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
        var ranges = new List<LanguageFoldingRange>(spans.Count);
        // Two constructs can share one delimiter pair's lines (an initializer opened and closed on the
        // same lines as the block containing it); the editor gets one range for those lines, not two.
        var emitted = new HashSet<LanguageFoldingRange>();
        foreach (var (start, close) in spans)
        {
            var startLine = lineMap.GetDocumentLine(start);
            var endLine = lineMap.GetDocumentLine(close) - 1;
            // A single-line construct, and one whose closing delimiter opens the line right after its
            // opening delimiter, fold nothing — and a range can never invert, whatever span the parse
            // produced.
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

    // The #region/#endregion pairs, matched innermost-first over the directives in source order. An
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

        AddSpan(parse, spans, open.SpanStart, close.SpanStart);
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
