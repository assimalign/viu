using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Parses a qualified rule's prelude token range into a <see cref="CssSelectorListNode"/> — comma-separated
/// complex selectors, each a flat source-order list of simple selectors, pseudo selectors, and the
/// combinators between compounds. This is the "sufficient for scoping decisions" selector parse the
/// scoped rewrite reads: it recovers the same node shape Vue's plugin walks over
/// <c>postcss-selector-parser</c>'s output (<c>@vue/compiler-sfc</c> <c>pluginScoped.ts</c>), including
/// the reserved functional pseudos <c>:deep()</c>/<c>:slotted()</c>/<c>:global()</c> whose inner selector
/// lists are parsed recursively. It follows the W3C Selectors Level 4 grammar
/// (https://www.w3.org/TR/selectors-4/) only as far as scoping needs — attribute-selector internals and
/// non-reserved functional pseudo arguments are kept as verbatim text, never re-parsed.
/// </summary>
internal sealed class CssSelectorParser
{
    private readonly string source;
    private readonly CssPositionMap positions;
    private readonly IReadOnlyList<CssToken> tokens;

    private CssSelectorParser(string source, CssPositionMap positions, IReadOnlyList<CssToken> tokens)
    {
        this.source = source;
        this.positions = positions;
        this.tokens = tokens;
    }

    /// <summary>
    /// Parses the tokens in <c>[start, end)</c> into a selector list located over the trimmed prelude.
    /// </summary>
    /// <param name="source">The CSS source string.</param>
    /// <param name="positions">The shared position map.</param>
    /// <param name="tokens">The full token stream.</param>
    /// <param name="start">The inclusive first prelude token index.</param>
    /// <param name="end">The exclusive last prelude token index.</param>
    /// <returns>The parsed selector list.</returns>
    public static CssSelectorListNode Parse(
        string source,
        CssPositionMap positions,
        IReadOnlyList<CssToken> tokens,
        int start,
        int end)
        => new CssSelectorParser(source, positions, tokens).ParseList(start, end);

    private CssSelectorListNode ParseList(int start, int end)
    {
        var complexSelectors = new List<CssComplexSelectorNode>();

        var segmentStart = start;
        for (var index = start; index < end; index++)
        {
            if (tokens[index].Kind == CssTokenKind.Comma)
            {
                var complex = ParseComplex(segmentStart, index);
                if (complex is not null)
                {
                    complexSelectors.Add(complex);
                }

                segmentStart = index + 1;
            }
        }

        var last = ParseComplex(segmentStart, end);
        if (last is not null)
        {
            complexSelectors.Add(last);
        }

        var location = TrimmedLocation(start, end);
        return new CssSelectorListNode
        {
            Selectors = new SyntaxList<CssComplexSelectorNode>(complexSelectors.ToArray()),
            Location = location,
        };
    }

    private CssComplexSelectorNode? ParseComplex(int start, int end)
    {
        // Trim insignificant whitespace/comments at both edges of the complex selector.
        var first = SkipTrivia(start, end);
        if (first >= end)
        {
            return null;
        }

        var parts = new List<CssSelectorPartNode>();
        var index = first;
        var pendingWhitespaceStart = -1;
        var pendingWhitespaceEnd = -1;

        while (index < end)
        {
            var token = tokens[index];
            if (token.Kind == CssTokenKind.Whitespace || token.Kind == CssTokenKind.Comment)
            {
                if (pendingWhitespaceStart < 0)
                {
                    pendingWhitespaceStart = token.Start;
                }

                pendingWhitespaceEnd = token.End;
                index++;
                continue;
            }

            if (TryReadExplicitCombinator(token, out var combinatorKind))
            {
                parts.Add(new CssCombinatorNode
                {
                    Combinator = combinatorKind,
                    Location = positions.LocationOf(token.Start, token.End),
                });
                pendingWhitespaceStart = -1;
                pendingWhitespaceEnd = -1;
                index++;
                continue;
            }

            // A compound piece follows. If whitespace separated it from a preceding compound (not a
            // combinator), that whitespace is a descendant combinator.
            if (pendingWhitespaceStart >= 0 && parts.Count > 0 && parts[parts.Count - 1] is not CssCombinatorNode)
            {
                parts.Add(new CssCombinatorNode
                {
                    Combinator = CssCombinatorKind.Descendant,
                    Location = positions.LocationOf(pendingWhitespaceStart, pendingWhitespaceEnd),
                });
            }

            pendingWhitespaceStart = -1;
            pendingWhitespaceEnd = -1;

            var part = ReadCompoundPiece(ref index, end);
            parts.Add(part);
        }

        var complexLocation = TrimmedLocation(start, end);
        return new CssComplexSelectorNode
        {
            Parts = new SyntaxList<CssSelectorPartNode>(parts.ToArray()),
            Location = complexLocation,
        };
    }

    private CssSelectorPartNode ReadCompoundPiece(ref int index, int end)
    {
        var token = tokens[index];

        switch (token.Kind)
        {
            case CssTokenKind.Hash:
                index++;
                return SimpleSelector(CssSimpleSelectorKind.Id, token.Start, token.End);

            case CssTokenKind.Ident:
                index++;
                return SimpleSelector(CssSimpleSelectorKind.Type, token.Start, token.End);

            case CssTokenKind.LeftBracket:
            {
                var closeEnd = ScanBalanced(index, end, CssTokenKind.LeftBracket, CssTokenKind.RightBracket);
                var start = token.Start;
                index = closeEnd.NextIndex;
                return SimpleSelector(CssSimpleSelectorKind.Attribute, start, closeEnd.EndOffset);
            }

            case CssTokenKind.Colon:
                return ReadPseudo(ref index, end);

            case CssTokenKind.Delim:
            {
                var text = source[token.Start];
                if (text == '.' && index + 1 < end && tokens[index + 1].Kind == CssTokenKind.Ident)
                {
                    var identEnd = tokens[index + 1].End;
                    var start = token.Start;
                    index += 2;
                    return SimpleSelector(CssSimpleSelectorKind.Class, start, identEnd);
                }

                if (text == '*')
                {
                    index++;
                    return SimpleSelector(CssSimpleSelectorKind.Universal, token.Start, token.End);
                }

                // An unrecognized delimiter (e.g. a namespace bar) is preserved verbatim as a type piece
                // so serialization never drops source; it is not the scoped attribute anchor of interest.
                index++;
                return SimpleSelector(CssSimpleSelectorKind.Type, token.Start, token.End);
            }

            default:
                // Numbers or stray tokens in a selector are malformed; preserve them verbatim.
                index++;
                return SimpleSelector(CssSimpleSelectorKind.Type, token.Start, token.End);
        }
    }

    private CssSelectorPartNode ReadPseudo(ref int index, int end)
    {
        var startOffset = tokens[index].Start;
        index++; // first colon
        var isElement = false;
        if (index < end && tokens[index].Kind == CssTokenKind.Colon)
        {
            isElement = true;
            index++;
        }

        if (index >= end)
        {
            return new CssPseudoSelectorNode
            {
                Pseudo = CssPseudoSelectorKind.Normal,
                Name = string.Empty,
                IsElement = isElement,
                Argument = null,
                Location = positions.LocationOf(startOffset, tokens[index - 1].End),
            };
        }

        var nameToken = tokens[index];
        if (nameToken.Kind == CssTokenKind.Function)
        {
            // The function token spans "name(" — the name excludes the trailing '('.
            var name = source.Substring(nameToken.Start, nameToken.Length - 1);
            var pseudoKind = ClassifyPseudo(name);
            var innerStart = index + 1;
            var close = ScanFunction(index, end);
            var innerEnd = close.CloseIndex; // exclusive of the ')'
            index = close.NextIndex;

            CssSelectorListNode? argument = null;
            if (pseudoKind != CssPseudoSelectorKind.Normal)
            {
                argument = ParseList(innerStart, innerEnd);
            }

            return new CssPseudoSelectorNode
            {
                Pseudo = pseudoKind,
                Name = name,
                IsElement = isElement,
                Argument = argument,
                Location = positions.LocationOf(startOffset, close.EndOffset),
            };
        }

        if (nameToken.Kind == CssTokenKind.Ident)
        {
            var name = source.Substring(nameToken.Start, nameToken.Length);
            index++;
            return new CssPseudoSelectorNode
            {
                Pseudo = ClassifyPseudo(name),
                Name = name,
                IsElement = isElement,
                Argument = null,
                Location = positions.LocationOf(startOffset, nameToken.End),
            };
        }

        // A lone colon (malformed); keep it verbatim as a normal pseudo with no name.
        return new CssPseudoSelectorNode
        {
            Pseudo = CssPseudoSelectorKind.Normal,
            Name = string.Empty,
            IsElement = isElement,
            Argument = null,
            Location = positions.LocationOf(startOffset, tokens[index - 1].End),
        };
    }

    private static CssPseudoSelectorKind ClassifyPseudo(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "deep":
            case "v-deep":
                return CssPseudoSelectorKind.Deep;
            case "slotted":
            case "v-slotted":
                return CssPseudoSelectorKind.Slotted;
            case "global":
            case "v-global":
                return CssPseudoSelectorKind.Global;
            default:
                return CssPseudoSelectorKind.Normal;
        }
    }

    private CssSimpleSelectorNode SimpleSelector(CssSimpleSelectorKind kind, int start, int end)
        => new CssSimpleSelectorNode
        {
            Selector = kind,
            Text = source.Substring(start, end - start),
            Location = positions.LocationOf(start, end),
        };

    private bool TryReadExplicitCombinator(CssToken token, out CssCombinatorKind kind)
    {
        if (token.Kind == CssTokenKind.Delim)
        {
            switch (source[token.Start])
            {
                case '>':
                    kind = CssCombinatorKind.Child;
                    return true;
                case '+':
                    kind = CssCombinatorKind.NextSibling;
                    return true;
                case '~':
                    kind = CssCombinatorKind.SubsequentSibling;
                    return true;
            }
        }

        kind = default;
        return false;
    }

    // Scans a balanced open/close bracket run starting at the open token; returns the index just past the
    // close, the exclusive index of the close token, and the close token's end offset. Recovers at end of
    // range when unbalanced (returns the range end).
    private BalancedScan ScanBalanced(int openIndex, int end, CssTokenKind open, CssTokenKind close)
    {
        var depth = 0;
        for (var index = openIndex; index < end; index++)
        {
            if (tokens[index].Kind == open)
            {
                depth++;
            }
            else if (tokens[index].Kind == close)
            {
                depth--;
                if (depth == 0)
                {
                    return new BalancedScan(index, index + 1, tokens[index].End);
                }
            }
        }

        // Unbalanced: recover by treating the whole remaining range as the enclosed content.
        var lastEnd = end > openIndex ? tokens[end - 1].End : tokens[openIndex].End;
        return new BalancedScan(end, end, lastEnd);
    }

    // Scans a functional pseudo's arguments to the matching ')'. The opening '(' rides inside the
    // <function-token> at functionIndex (and every nested <function-token> opens another paren), so depth
    // seeds at one and counts Function and '(' tokens as openers.
    private BalancedScan ScanFunction(int functionIndex, int end)
    {
        var depth = 1;
        for (var index = functionIndex + 1; index < end; index++)
        {
            var kind = tokens[index].Kind;
            if (kind == CssTokenKind.Function || kind == CssTokenKind.LeftParenthesis)
            {
                depth++;
            }
            else if (kind == CssTokenKind.RightParenthesis)
            {
                depth--;
                if (depth == 0)
                {
                    return new BalancedScan(index, index + 1, tokens[index].End);
                }
            }
        }

        var lastEnd = end > functionIndex ? tokens[end - 1].End : tokens[functionIndex].End;
        return new BalancedScan(end, end, lastEnd);
    }

    private int SkipTrivia(int start, int end)
    {
        var index = start;
        while (index < end && (tokens[index].Kind == CssTokenKind.Whitespace || tokens[index].Kind == CssTokenKind.Comment))
        {
            index++;
        }

        return index;
    }

    // The location spanning the trimmed significant token range [firstSignificant.Start,
    // lastSignificant.End); falls back to the raw range edges when there are no significant tokens
    // (empty selector).
    private SourceLocation TrimmedLocation(int start, int end)
    {
        var firstSignificant = SkipTrivia(start, end);
        if (firstSignificant >= end)
        {
            var edgeStart = start < tokens.Count ? tokens[start].Start : positions.Length;
            return positions.LocationOf(edgeStart, edgeStart);
        }

        var lastSignificant = end - 1;
        while (lastSignificant > firstSignificant
            && (tokens[lastSignificant].Kind == CssTokenKind.Whitespace || tokens[lastSignificant].Kind == CssTokenKind.Comment))
        {
            lastSignificant--;
        }

        return positions.LocationOf(tokens[firstSignificant].Start, tokens[lastSignificant].End);
    }

    private readonly record struct BalancedScan(int CloseIndex, int NextIndex, int EndOffset);
}
