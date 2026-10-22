using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The recursive-descent rule-level parser that turns the <see cref="CssTokenizer"/> token stream into a
/// <see cref="CssStylesheetNode"/> tree, following the CSS Syntax Module Level 3 parsing algorithms
/// (https://www.w3.org/TR/css-syntax-3/#parsing): a stylesheet is a list of rules; a qualified rule is a
/// selector prelude plus a declaration block; an at-rule is an at-keyword, a prelude, and either a block
/// or a <c>;</c>. Conditional-group at-rules (<c>@media</c>/<c>@supports</c>/<c>@container</c>) recurse
/// into nested rules and <c>@keyframes</c> into keyframe rules. Parsing is context-directed rather than a
/// full generic component-value tree — enough to make correct scoping decisions — and is recoverable:
/// malformed input reports a <see cref="CssError"/> and the parser resynchronizes; it never throws.
/// </summary>
internal sealed class CssParseEngine
{
    private readonly string source;
    private readonly CssPositionMap positions;
    private readonly IReadOnlyList<CssToken> tokens;
    private readonly List<CssError> diagnostics;
    private int position;

    public CssParseEngine(string source, CssPositionMap positions, IReadOnlyList<CssToken> tokens, List<CssError> diagnostics)
    {
        this.source = source;
        this.positions = positions;
        this.tokens = tokens;
        this.diagnostics = diagnostics;
    }

    /// <summary>Parses the whole token stream into a stylesheet whose location spans the entire source.</summary>
    /// <returns>The parsed stylesheet root.</returns>
    public CssStylesheetNode ParseStylesheet()
    {
        var rules = ParseRuleList(stopAtRightBrace: false);
        return new CssStylesheetNode
        {
            Rules = new SyntaxList<CssSyntaxNode>(rules.ToArray()),
            Location = positions.LocationOf(0, positions.Length),
        };
    }

    private List<CssSyntaxNode> ParseRuleList(bool stopAtRightBrace)
    {
        var rules = new List<CssSyntaxNode>();
        while (true)
        {
            SkipTrivia();
            var token = Current;
            if (token.Kind == CssTokenKind.EndOfFile)
            {
                break;
            }

            if (token.Kind == CssTokenKind.RightBrace)
            {
                if (stopAtRightBrace)
                {
                    position++; // consume the closing brace of the enclosing block
                    break;
                }

                // A stray '}' at the top level is a parse error; discard it and resynchronize.
                diagnostics.Add(new CssError(CssErrorCode.UnexpectedRightBrace, positions.LocationOf(token.Start, token.End)));
                position++;
                continue;
            }

            if (token.Kind == CssTokenKind.AtKeyword)
            {
                rules.Add(ParseAtRule());
            }
            else
            {
                var rule = ParseQualifiedRule();
                if (rule is not null)
                {
                    rules.Add(rule);
                }
            }
        }

        return rules;
    }

    private CssSyntaxNode? ParseQualifiedRule()
    {
        var start = Current.Start;
        var preludeStart = position;
        var terminator = ScanTo(stopAtSemicolon: false);

        if (terminator == ScanResult.EndOfFile)
        {
            // A qualified rule with no block before end of file is discarded (recoverable).
            diagnostics.Add(new CssError(CssErrorCode.UnexpectedEndOfFile, positions.LocationOf(start, positions.Length)));
            return null;
        }

        var preludeEnd = position; // the '{' token index
        var selectors = CssSelectorParser.Parse(source, positions, tokens, preludeStart, preludeEnd);
        if (selectors.Selectors.Count == 0)
        {
            var braceStart = tokens[preludeEnd].Start;
            diagnostics.Add(new CssError(CssErrorCode.EmptySelector, positions.LocationOf(start, braceStart)));
        }

        var declarations = ParseDeclarationBlock(out var blockEnd);
        return new CssQualifiedRuleNode
        {
            Prelude = TrimmedText(preludeStart, preludeEnd),
            Selectors = selectors,
            Declarations = new SyntaxList<CssDeclarationNode>(declarations.ToArray()),
            Location = positions.LocationOf(start, blockEnd),
        };
    }

    private CssSyntaxNode ParseAtRule()
    {
        var atToken = Current;
        var start = atToken.Start;
        var name = source.Substring(atToken.Start + 1, atToken.Length - 1); // strip the leading '@'
        position++; // consume the at-keyword
        var preludeStart = position;
        var terminator = ScanTo(stopAtSemicolon: true);

        if (terminator == ScanResult.Semicolon)
        {
            var semicolon = Current;
            var semicolonIndex = position;
            position++; // consume ';'
            return new CssAtRuleNode
            {
                Name = name,
                Prelude = TrimmedText(preludeStart, semicolonIndex),
                HasBlock = false,
                Body = SyntaxList<CssSyntaxNode>.Empty,
                Location = positions.LocationOf(start, semicolon.End),
            };
        }

        if (terminator == ScanResult.EndOfFile)
        {
            // A statement at-rule with no ';' before end of file; recover as a blockless at-rule.
            return new CssAtRuleNode
            {
                Name = name,
                Prelude = TrimmedText(preludeStart, position),
                HasBlock = false,
                Body = SyntaxList<CssSyntaxNode>.Empty,
                Location = positions.LocationOf(start, positions.Length),
            };
        }

        var preludeEnd = position; // the '{' token index
        var prelude = TrimmedText(preludeStart, preludeEnd);
        var body = ParseAtRuleBody(name, out var blockEnd);
        return new CssAtRuleNode
        {
            Name = name,
            Prelude = prelude,
            HasBlock = true,
            Body = new SyntaxList<CssSyntaxNode>(body.ToArray()),
            Location = positions.LocationOf(start, blockEnd),
        };
    }

    private List<CssSyntaxNode> ParseAtRuleBody(string name, out int blockEnd)
    {
        var bare = DeVendorPrefix(name);
        if (string.Equals(bare, "keyframes", StringComparison.OrdinalIgnoreCase))
        {
            return ParseKeyframeBlock(out blockEnd);
        }

        if (IsConditionalGroup(bare))
        {
            position++; // consume '{'
            var nested = ParseRuleList(stopAtRightBrace: true);
            blockEnd = position > 0 ? tokens[position - 1].End : positions.Length;
            return nested;
        }

        // Declaration-only at-rules (@font-face, @page, @property, …) and unknown block at-rules.
        var declarations = ParseDeclarationBlock(out blockEnd);
        var body = new List<CssSyntaxNode>(declarations.Count);
        foreach (var declaration in declarations)
        {
            body.Add(declaration);
        }

        return body;
    }

    private List<CssSyntaxNode> ParseKeyframeBlock(out int blockEnd)
    {
        var rules = new List<CssSyntaxNode>();
        position++; // consume '{'
        while (true)
        {
            SkipTrivia();
            var token = Current;
            if (token.Kind == CssTokenKind.EndOfFile)
            {
                diagnostics.Add(new CssError(CssErrorCode.UnterminatedBlock, positions.LocationOf(token.Start, token.End)));
                break;
            }

            if (token.Kind == CssTokenKind.RightBrace)
            {
                position++;
                break;
            }

            var start = token.Start;
            var selectorStart = position;
            var terminator = ScanTo(stopAtSemicolon: false);
            if (terminator == ScanResult.EndOfFile)
            {
                diagnostics.Add(new CssError(CssErrorCode.UnterminatedBlock, positions.LocationOf(start, positions.Length)));
                break;
            }

            var selectorText = TrimmedText(selectorStart, position);
            var declarations = ParseDeclarationBlock(out var keyframeEnd);
            rules.Add(new CssKeyframeRuleNode
            {
                Selector = selectorText,
                Declarations = new SyntaxList<CssDeclarationNode>(declarations.ToArray()),
                Location = positions.LocationOf(start, keyframeEnd),
            });
        }

        blockEnd = position > 0 ? tokens[position - 1].End : positions.Length;
        return rules;
    }

    // Parses a '{ … }' declaration block; assumes the current token is the '{'. Sets blockEnd to the
    // offset just past the closing '}' (or end of source when unterminated).
    private List<CssDeclarationNode> ParseDeclarationBlock(out int blockEnd)
    {
        var declarations = new List<CssDeclarationNode>();
        position++; // consume '{'
        while (true)
        {
            SkipTriviaAndSemicolons();
            var token = Current;
            if (token.Kind == CssTokenKind.EndOfFile)
            {
                diagnostics.Add(new CssError(CssErrorCode.UnterminatedBlock, positions.LocationOf(token.Start, token.End)));
                blockEnd = positions.Length;
                return declarations;
            }

            if (token.Kind == CssTokenKind.RightBrace)
            {
                var end = token.End;
                position++;
                blockEnd = end;
                return declarations;
            }

            var declaration = ParseDeclaration();
            if (declaration is not null)
            {
                declarations.Add(declaration);
            }
        }
    }

    private CssDeclarationNode? ParseDeclaration()
    {
        var runStart = position;
        // Collect tokens up to the next top-level ';' or '}'.
        var colonIndex = -1;
        var depth = 0;
        while (true)
        {
            var token = Current;
            if (token.Kind == CssTokenKind.EndOfFile)
            {
                break;
            }

            if (depth == 0 && (token.Kind == CssTokenKind.Semicolon || token.Kind == CssTokenKind.RightBrace))
            {
                break;
            }

            if (token.Kind == CssTokenKind.LeftParenthesis || token.Kind == CssTokenKind.Function || token.Kind == CssTokenKind.LeftBracket || token.Kind == CssTokenKind.LeftBrace)
            {
                depth++;
            }
            else if (token.Kind == CssTokenKind.RightParenthesis || token.Kind == CssTokenKind.RightBracket || token.Kind == CssTokenKind.RightBrace)
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (depth == 0 && token.Kind == CssTokenKind.Colon && colonIndex < 0)
            {
                colonIndex = position;
            }

            position++;
        }

        var runEnd = position; // the ';' / '}' / EOF token index
        if (Current.Kind == CssTokenKind.Semicolon)
        {
            position++; // consume the ';' so the next declaration starts cleanly
        }

        if (colonIndex < 0)
        {
            var runLocation = SignificantLocation(runStart, runEnd);
            if (runLocation is { } location)
            {
                diagnostics.Add(new CssError(CssErrorCode.MissingDeclarationColon, location));
            }

            return null;
        }

        var property = TrimmedText(runStart, colonIndex);
        var valueRaw = TrimmedText(colonIndex + 1, runEnd);
        var important = TryStripImportant(ref valueRaw);
        var declarationLocation = SignificantLocation(runStart, runEnd)
            ?? positions.LocationOf(tokens[runStart].Start, tokens[runStart].Start);
        return new CssDeclarationNode
        {
            Property = property,
            Value = valueRaw,
            Important = important,
            Location = declarationLocation,
        };
    }

    // Advances position to the next top-level '{' (always a stop) and, when stopAtSemicolon, ';'.
    // Respects (), function, [], and string nesting so a brace inside them does not end the prelude.
    private ScanResult ScanTo(bool stopAtSemicolon)
    {
        var depth = 0;
        while (true)
        {
            var token = Current;
            switch (token.Kind)
            {
                case CssTokenKind.EndOfFile:
                    return ScanResult.EndOfFile;
                case CssTokenKind.LeftParenthesis:
                case CssTokenKind.Function:
                case CssTokenKind.LeftBracket:
                    depth++;
                    break;
                case CssTokenKind.RightParenthesis:
                case CssTokenKind.RightBracket:
                    if (depth > 0)
                    {
                        depth--;
                    }

                    break;
                case CssTokenKind.LeftBrace when depth == 0:
                    return ScanResult.LeftBrace;
                case CssTokenKind.Semicolon when depth == 0 && stopAtSemicolon:
                    return ScanResult.Semicolon;
            }

            position++;
        }
    }

    private CssToken Current => tokens[position];

    private void SkipTrivia()
    {
        while (Current.Kind == CssTokenKind.Whitespace || Current.Kind == CssTokenKind.Comment)
        {
            position++;
        }
    }

    private void SkipTriviaAndSemicolons()
    {
        while (Current.Kind == CssTokenKind.Whitespace || Current.Kind == CssTokenKind.Comment || Current.Kind == CssTokenKind.Semicolon)
        {
            position++;
        }
    }

    // The trimmed raw text of the token range [start, end); empty when the range holds only trivia.
    private string TrimmedText(int start, int end)
    {
        var location = SignificantLocation(start, end);
        return location?.Source ?? string.Empty;
    }

    // The location spanning the significant token range within [start, end), or null when it is all trivia.
    private SourceLocation? SignificantLocation(int start, int end)
    {
        var first = start;
        while (first < end && (tokens[first].Kind == CssTokenKind.Whitespace || tokens[first].Kind == CssTokenKind.Comment))
        {
            first++;
        }

        if (first >= end)
        {
            return null;
        }

        var last = end - 1;
        while (last > first && (tokens[last].Kind == CssTokenKind.Whitespace || tokens[last].Kind == CssTokenKind.Comment))
        {
            last--;
        }

        return positions.LocationOf(tokens[first].Start, tokens[last].End);
    }

    // Strips a trailing "!important" (allowing whitespace between '!' and the keyword) from the value,
    // returning whether one was present. Case-insensitive per CSS.
    private static bool TryStripImportant(ref string value)
    {
        var trimmed = value.TrimEnd();
        var bang = trimmed.LastIndexOf('!');
        if (bang < 0)
        {
            return false;
        }

        var flag = trimmed.Substring(bang + 1).TrimStart();
        if (!string.Equals(flag, "important", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = trimmed.Substring(0, bang).TrimEnd();
        return true;
    }

    private static string DeVendorPrefix(string name)
    {
        // Strip a single leading vendor prefix like "-webkit-" / "-moz-" so "@-webkit-keyframes" scopes.
        if (name.Length > 1 && name[0] == '-')
        {
            var secondDash = name.IndexOf('-', 1);
            if (secondDash > 0 && secondDash < name.Length - 1)
            {
                return name.Substring(secondDash + 1);
            }
        }

        return name;
    }

    private static bool IsConditionalGroup(string bareName)
        => string.Equals(bareName, "media", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bareName, "supports", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bareName, "container", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bareName, "layer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bareName, "document", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bareName, "scope", StringComparison.OrdinalIgnoreCase);

    private enum ScanResult
    {
        LeftBrace,
        Semicolon,
        EndOfFile,
    }
}
