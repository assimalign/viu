using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.VisualStudio;

internal static class ViuLexicalClassifier
{
    private static readonly Regex SectionHeaderExpression = new(
        @"^\s*@(?<name>template|script|style)\b[^{]*(?<brace>\{)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateTagExpression = new(
        @"</?(?<name>[A-Za-z][A-Za-z0-9_.:-]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateAttributeExpression = new(
        @"(?<name>@?[A-Za-z_:][A-Za-z0-9_.:-]*)\s*=\s*(?<value>""[^""]*""|'[^']*')",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateInterpolationExpression = new(
        @"\{\{|\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScriptStringExpression = new(
        @"@?""(?:""""|\\.|[^""])*""|'(?:\\.|[^'\\])'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScriptKeywordExpression = new(
        @"\b(?:abstract|as|async|await|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|from|get|global|goto|if|implicit|in|init|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|partial|private|protected|public|readonly|record|ref|required|return|sbyte|sealed|set|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|when|where|while|with|yield)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScriptTypeExpression = new(
        @"\b[A-Z][A-Za-z0-9_]*(?:<[^>\r\n]+>)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScriptMethodExpression = new(
        @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?=\()",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumberExpression = new(
        @"(?<![A-Za-z0-9_])(?:0[xX][0-9A-Fa-f]+|\d+(?:\.\d+)?)(?:[uUlLfFdDmM]+)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StyleStringExpression = new(
        @"""(?:\\.|[^""])*""|'(?:\\.|[^'])*'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StylePropertyExpression = new(
        @"(?<name>--[A-Za-z0-9_-]+|[A-Za-z-]+)\s*(?=:)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StyleAtRuleExpression = new(
        @"@[A-Za-z-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StyleSelectorExpression = new(
        @"^\s*(?<selector>[^@{}\s][^{]*)(?=\{)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<ViuLexicalSpan> Classify(IReadOnlyList<string> lines)
    {
        List<ViuLexicalSpan> spans = [];
        ViuSectionKind sectionKind = ViuSectionKind.None;
        bool isInScriptComment = false;
        bool isInStyleComment = false;
        bool isInTemplateComment = false;

        for (int lineNumber = 0; lineNumber < lines.Count; lineNumber++)
        {
            string line = lines[lineNumber];
            bool[] occupiedCharacters = new bool[line.Length];
            Match sectionMatch = SectionHeaderExpression.Match(line);

            if (sectionMatch.Success)
            {
                string sectionName = sectionMatch.Groups["name"].Value;
                sectionKind = sectionName switch
                {
                    "template" => ViuSectionKind.Template,
                    "script" => ViuSectionKind.Script,
                    "style" => ViuSectionKind.Style,
                    _ => ViuSectionKind.None,
                };

                Group nameGroup = sectionMatch.Groups["name"];
                AddSpan(
                    lineNumber,
                    nameGroup.Index - 1,
                    nameGroup.Length + 1,
                    ViuClassificationKind.Keyword,
                    occupiedCharacters,
                    spans);

                Group braceGroup = sectionMatch.Groups["brace"];
                AddSpan(
                    lineNumber,
                    braceGroup.Index,
                    braceGroup.Length,
                    ViuClassificationKind.Punctuation,
                    occupiedCharacters,
                    spans);
            }

            switch (sectionKind)
            {
                case ViuSectionKind.Template:
                    ClassifyTemplateLine(
                        line,
                        lineNumber,
                        ref isInTemplateComment,
                        occupiedCharacters,
                        spans);
                    break;

                case ViuSectionKind.Script:
                    ClassifyScriptLine(
                        line,
                        lineNumber,
                        ref isInScriptComment,
                        occupiedCharacters,
                        spans);
                    break;

                case ViuSectionKind.Style:
                    ClassifyStyleLine(
                        line,
                        lineNumber,
                        ref isInStyleComment,
                        occupiedCharacters,
                        spans);
                    break;
            }
        }

        return spans;
    }

    private static void ClassifyTemplateLine(
        string line,
        int lineNumber,
        ref bool isInComment,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        ClassifyDelimitedComments(
            line,
            lineNumber,
            "<!--",
            "-->",
            ref isInComment,
            occupiedCharacters,
            spans);

        foreach (Match match in TemplateAttributeExpression.Matches(line))
        {
            Group nameGroup = match.Groups["name"];
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                nameGroup.Value.StartsWith("@", StringComparison.Ordinal)
                    ? ViuClassificationKind.Keyword
                    : ViuClassificationKind.MarkupAttribute,
                occupiedCharacters,
                spans);

            Group valueGroup = match.Groups["value"];
            AddSpan(
                lineNumber,
                valueGroup.Index,
                valueGroup.Length,
                ViuClassificationKind.MarkupAttributeValue,
                occupiedCharacters,
                spans);
        }

        foreach (Match match in TemplateTagExpression.Matches(line))
        {
            Group nameGroup = match.Groups["name"];
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                ViuClassificationKind.MarkupNode,
                occupiedCharacters,
                spans);
        }

        foreach (Match match in TemplateInterpolationExpression.Matches(line))
        {
            AddSpan(
                lineNumber,
                match.Index,
                match.Length,
                ViuClassificationKind.Punctuation,
                occupiedCharacters,
                spans);
        }

        ClassifyCharacters(
            line,
            lineNumber,
            "<>/=",
            ViuClassificationKind.Operator,
            occupiedCharacters,
            spans);
    }

    private static void ClassifyScriptLine(
        string line,
        int lineNumber,
        ref bool isInComment,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        if (isInComment)
        {
            ClassifyDelimitedComments(
                line,
                lineNumber,
                "/*",
                "*/",
                ref isInComment,
                occupiedCharacters,
                spans);
        }

        ClassifyMatches(
            lineNumber,
            ScriptStringExpression.Matches(line),
            ViuClassificationKind.String,
            occupiedCharacters,
            spans);

        ClassifyDelimitedComments(
            line,
            lineNumber,
            "/*",
            "*/",
            ref isInComment,
            occupiedCharacters,
            spans);

        int lineCommentStart = FindUnoccupiedToken(line, "//", occupiedCharacters);
        if (lineCommentStart >= 0)
        {
            AddSpan(
                lineNumber,
                lineCommentStart,
                line.Length - lineCommentStart,
                ViuClassificationKind.Comment,
                occupiedCharacters,
                spans);
        }

        ClassifyMatches(
            lineNumber,
            ScriptKeywordExpression.Matches(line),
            ViuClassificationKind.Keyword,
            occupiedCharacters,
            spans);

        foreach (Match match in ScriptMethodExpression.Matches(line))
        {
            Group nameGroup = match.Groups["name"];
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                ViuClassificationKind.Method,
                occupiedCharacters,
                spans);
        }

        ClassifyMatches(
            lineNumber,
            ScriptTypeExpression.Matches(line),
            ViuClassificationKind.Type,
            occupiedCharacters,
            spans);

        ClassifyMatches(
            lineNumber,
            NumberExpression.Matches(line),
            ViuClassificationKind.Number,
            occupiedCharacters,
            spans);

        ClassifyCharacters(
            line,
            lineNumber,
            "{}[]();,.<>",
            ViuClassificationKind.Punctuation,
            occupiedCharacters,
            spans);

        ClassifyCharacters(
            line,
            lineNumber,
            "+-*/%=!&|?:",
            ViuClassificationKind.Operator,
            occupiedCharacters,
            spans);
    }

    private static void ClassifyStyleLine(
        string line,
        int lineNumber,
        ref bool isInComment,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        if (isInComment)
        {
            ClassifyDelimitedComments(
                line,
                lineNumber,
                "/*",
                "*/",
                ref isInComment,
                occupiedCharacters,
                spans);
        }

        ClassifyMatches(
            lineNumber,
            StyleStringExpression.Matches(line),
            ViuClassificationKind.String,
            occupiedCharacters,
            spans);

        ClassifyDelimitedComments(
            line,
            lineNumber,
            "/*",
            "*/",
            ref isInComment,
            occupiedCharacters,
            spans);

        ClassifyMatches(
            lineNumber,
            StyleAtRuleExpression.Matches(line),
            ViuClassificationKind.Keyword,
            occupiedCharacters,
            spans);

        foreach (Match match in StylePropertyExpression.Matches(line))
        {
            Group nameGroup = match.Groups["name"];
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                ViuClassificationKind.MarkupAttribute,
                occupiedCharacters,
                spans);
        }

        foreach (Match match in StyleSelectorExpression.Matches(line))
        {
            Group selectorGroup = match.Groups["selector"];
            AddSpan(
                lineNumber,
                selectorGroup.Index,
                selectorGroup.Length,
                ViuClassificationKind.MarkupNode,
                occupiedCharacters,
                spans);
        }

        ClassifyMatches(
            lineNumber,
            NumberExpression.Matches(line),
            ViuClassificationKind.Number,
            occupiedCharacters,
            spans);

        ClassifyCharacters(
            line,
            lineNumber,
            "{}[]();,:",
            ViuClassificationKind.Punctuation,
            occupiedCharacters,
            spans);
    }

    private static void ClassifyDelimitedComments(
        string line,
        int lineNumber,
        string startToken,
        string endToken,
        ref bool isInComment,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        int searchStart = 0;

        while (searchStart < line.Length)
        {
            int commentStart = isInComment
                ? searchStart
                : FindUnoccupiedToken(line, startToken, occupiedCharacters, searchStart);

            if (commentStart < 0)
            {
                return;
            }

            int endSearchStart = isInComment
                ? searchStart
                : commentStart + startToken.Length;
            int commentEnd = line.IndexOf(endToken, endSearchStart, StringComparison.Ordinal);

            if (commentEnd < 0)
            {
                AddSpan(
                    lineNumber,
                    commentStart,
                    line.Length - commentStart,
                    ViuClassificationKind.Comment,
                    occupiedCharacters,
                    spans);
                isInComment = true;
                return;
            }

            int length = commentEnd + endToken.Length - commentStart;
            AddSpan(
                lineNumber,
                commentStart,
                length,
                ViuClassificationKind.Comment,
                occupiedCharacters,
                spans);
            isInComment = false;
            searchStart = commentEnd + endToken.Length;
        }
    }

    private static void ClassifyMatches(
        int lineNumber,
        MatchCollection matches,
        ViuClassificationKind classificationKind,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        foreach (Match match in matches)
        {
            AddSpan(
                lineNumber,
                match.Index,
                match.Length,
                classificationKind,
                occupiedCharacters,
                spans);
        }
    }

    private static void ClassifyCharacters(
        string line,
        int lineNumber,
        string characters,
        ViuClassificationKind classificationKind,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        for (int characterIndex = 0; characterIndex < line.Length; characterIndex++)
        {
            if (characters.IndexOf(line[characterIndex], StringComparison.Ordinal) >= 0)
            {
                AddSpan(
                    lineNumber,
                    characterIndex,
                    1,
                    classificationKind,
                    occupiedCharacters,
                    spans);
            }
        }
    }

    private static int FindUnoccupiedToken(
        string line,
        string token,
        bool[] occupiedCharacters,
        int searchStart = 0)
    {
        while (searchStart < line.Length)
        {
            int tokenIndex = line.IndexOf(token, searchStart, StringComparison.Ordinal);
            if (tokenIndex < 0)
            {
                return -1;
            }

            if (IsAvailable(tokenIndex, token.Length, occupiedCharacters))
            {
                return tokenIndex;
            }

            searchStart = tokenIndex + token.Length;
        }

        return -1;
    }

    private static void AddSpan(
        int lineNumber,
        int start,
        int length,
        ViuClassificationKind classificationKind,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        if (length <= 0 ||
            start < 0 ||
            start + length > occupiedCharacters.Length ||
            !IsAvailable(start, length, occupiedCharacters))
        {
            return;
        }

        spans.Add(new(lineNumber, start, length, classificationKind));
        Array.Fill(occupiedCharacters, true, start, length);
    }

    private static bool IsAvailable(
        int start,
        int length,
        bool[] occupiedCharacters)
    {
        for (int characterIndex = start; characterIndex < start + length; characterIndex++)
        {
            if (occupiedCharacters[characterIndex])
            {
                return false;
            }
        }

        return true;
    }
}
