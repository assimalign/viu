using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.VisualStudio;

internal static class ViuLexicalClassifier
{
    // The hybrid .viu container ([V01.01.06.10], Assimalign.Viu.Syntax.SingleFileComponent
    // docs/FORMAT.md): <template> and <style> are top-level tags, @script keeps the @-block grammar,
    // and the legacy @template/@style blocks keep highlighting during the migration window.
    private static readonly Regex SectionHeaderExpression = new(
        @"^\s*@(?<name>template|script|style)\b[^{]*(?<brace>\{)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TagSectionOpenExpression = new(
        @"^\s*<(?<name>template|style|script)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateSectionTagExpression = new(
        @"</?template\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TagHeaderAttributeExpression = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_.:-]*)(?:\s*=\s*(?<value>""[^""]*""|'[^']*'))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateTagExpression = new(
        @"</?(?<name>[A-Za-z][A-Za-z0-9_.:-]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateAttributeExpression = new(
        @"(?<name>[@:#]?[A-Za-z_][A-Za-z0-9_.:-]*)\s*=\s*(?<value>""[^""]*""|'[^']*')",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateValuelessDirectiveExpression = new(
        @"(?<![A-Za-z0-9_""'-])(?:v-[A-Za-z0-9-]+|[@:#][A-Za-z][A-Za-z0-9_.:-]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TemplateInterpolationExpression = new(
        @"\{\{(?<content>.*?)\}\}",
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
        bool isTagDelimitedSection = false;
        int templateTagDepth = 0;
        bool isInScriptComment = false;
        bool isInStyleComment = false;
        bool isInTemplateComment = false;

        for (int lineNumber = 0; lineNumber < lines.Count; lineNumber++)
        {
            string line = lines[lineNumber];
            bool[] occupiedCharacters = new bool[line.Length];

            // Tag-delimited sections close at their matching end tag, never at an @-header or a
            // column-0 '}' — the closer alone decides where the section ends.
            if (isTagDelimitedSection)
            {
                switch (sectionKind)
                {
                    case ViuSectionKind.Template:
                        if (ClassifyTagDelimitedTemplateLine(
                                line,
                                lineNumber,
                                ref templateTagDepth,
                                ref isInTemplateComment,
                                occupiedCharacters,
                                spans))
                        {
                            sectionKind = ViuSectionKind.None;
                            isTagDelimitedSection = false;
                        }

                        break;

                    case ViuSectionKind.Style:
                        if (ClassifyTagDelimitedRawLine(
                                line,
                                lineNumber,
                                "</style>",
                                0,
                                ref isInStyleComment,
                                ClassifyStyleLine,
                                occupiedCharacters,
                                spans))
                        {
                            sectionKind = ViuSectionKind.None;
                            isTagDelimitedSection = false;
                        }

                        break;

                    case ViuSectionKind.Script:
                        if (ClassifyTagDelimitedRawLine(
                                line,
                                lineNumber,
                                "</script>",
                                0,
                                ref isInScriptComment,
                                ClassifyScriptLine,
                                occupiedCharacters,
                                spans))
                        {
                            sectionKind = ViuSectionKind.None;
                            isTagDelimitedSection = false;
                        }

                        break;
                }

                continue;
            }

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
            else if (sectionKind == ViuSectionKind.None)
            {
                Match tagMatch = TagSectionOpenExpression.Match(line);
                if (tagMatch.Success)
                {
                    ClassifyTagSectionOpenLine(
                        line,
                        lineNumber,
                        tagMatch,
                        ref sectionKind,
                        ref isTagDelimitedSection,
                        ref templateTagDepth,
                        ref isInTemplateComment,
                        ref isInScriptComment,
                        ref isInStyleComment,
                        occupiedCharacters,
                        spans);
                    continue;
                }
            }
            else if (line.Length > 0 && line[0] == '}')
            {
                // Column 0 is structural in the hybrid format: this '}' closes the open @-block.
                AddSpan(
                    lineNumber,
                    0,
                    1,
                    ViuClassificationKind.Punctuation,
                    occupiedCharacters,
                    spans);
                sectionKind = ViuSectionKind.None;
                continue;
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

    // Opens a tag-delimited section at the top level: classifies the opening tag header, selects the
    // section, and classifies any inline content (including a same-line closer) on the same line.
    private static void ClassifyTagSectionOpenLine(
        string line,
        int lineNumber,
        Match tagMatch,
        ref ViuSectionKind sectionKind,
        ref bool isTagDelimitedSection,
        ref int templateTagDepth,
        ref bool isInTemplateComment,
        ref bool isInScriptComment,
        ref bool isInStyleComment,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        Group nameGroup = tagMatch.Groups["name"];
        int tagStart = nameGroup.Index - 1;
        int headerClose = line.IndexOf('>', nameGroup.Index + nameGroup.Length);
        bool isSelfClosing = headerClose > 0 && line[headerClose - 1] == '/';
        int headerEnd = headerClose < 0 ? line.Length : headerClose + 1;

        ClassifyTagHeader(line, lineNumber, tagStart, headerEnd, occupiedCharacters, spans);

        if (isSelfClosing)
        {
            // A self-closing top-level tag carries no content, so no section opens.
            return;
        }

        switch (nameGroup.Value)
        {
            case "template":
                sectionKind = ViuSectionKind.Template;
                isTagDelimitedSection = true;
                templateTagDepth = 0;
                if (ClassifyTagDelimitedTemplateLine(
                        line,
                        lineNumber,
                        ref templateTagDepth,
                        ref isInTemplateComment,
                        occupiedCharacters,
                        spans))
                {
                    sectionKind = ViuSectionKind.None;
                    isTagDelimitedSection = false;
                }

                break;

            case "style":
                sectionKind = ViuSectionKind.Style;
                isTagDelimitedSection = true;
                if (ClassifyTagDelimitedRawLine(
                        line,
                        lineNumber,
                        "</style>",
                        headerEnd,
                        ref isInStyleComment,
                        ClassifyStyleLine,
                        occupiedCharacters,
                        spans))
                {
                    sectionKind = ViuSectionKind.None;
                    isTagDelimitedSection = false;
                }

                break;

            case "script":
                // The container parser rejects a top-level <script> tag (VIU1017); the lexer still
                // colors it as a script section so the misplaced code stays readable while the
                // diagnostic points at the fix.
                sectionKind = ViuSectionKind.Script;
                isTagDelimitedSection = true;
                if (ClassifyTagDelimitedRawLine(
                        line,
                        lineNumber,
                        "</script>",
                        headerEnd,
                        ref isInScriptComment,
                        ClassifyScriptLine,
                        occupiedCharacters,
                        spans))
                {
                    sectionKind = ViuSectionKind.None;
                    isTagDelimitedSection = false;
                }

                break;
        }
    }

    // Classifies one line of a tag-delimited template section, tracking nested <template> depth so a
    // slot fragment such as <template #header> does not end the section. Returns true when the true
    // closing </template> was found on this line; content after that closer stays unclassified.
    private static bool ClassifyTagDelimitedTemplateLine(
        string line,
        int lineNumber,
        ref int templateTagDepth,
        ref bool isInComment,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        int closerEnd = ScanTemplateTagDepth(line, ref templateTagDepth);
        if (closerEnd >= 0 && closerEnd < line.Length)
        {
            // Occupy everything past the true closer without emitting spans; whatever follows the
            // section is top-level text this line's template rules must not color.
            Array.Fill(occupiedCharacters, true, closerEnd, line.Length - closerEnd);
        }

        ClassifyTemplateLine(line, lineNumber, ref isInComment, occupiedCharacters, spans);
        return closerEnd >= 0;
    }

    // Classifies one line of a tag-delimited raw-text section (<style> or the rejected <script>).
    // Returns true when the closing tag was found on this line; the closer is classified as markup
    // and content after it stays unclassified.
    private static bool ClassifyTagDelimitedRawLine(
        string line,
        int lineNumber,
        string closingTag,
        int searchStart,
        ref bool isInComment,
        SectionLineClassifier classifyContent,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        int closerIndex = searchStart <= line.Length
            ? line.IndexOf(closingTag, searchStart, StringComparison.Ordinal)
            : -1;
        if (closerIndex >= 0)
        {
            ClassifyClosingTag(line, lineNumber, closerIndex, closingTag.Length, occupiedCharacters, spans);
            int afterCloser = closerIndex + closingTag.Length;
            if (afterCloser < line.Length)
            {
                Array.Fill(occupiedCharacters, true, afterCloser, line.Length - afterCloser);
            }
        }

        classifyContent(line, lineNumber, ref isInComment, occupiedCharacters, spans);
        return closerIndex >= 0;
    }

    private delegate void SectionLineClassifier(
        string line,
        int lineNumber,
        ref bool isInComment,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans);

    // Walks the <template>/</template> boundaries on a line, updating the nesting depth. Returns the
    // exclusive end offset of the closer that brings the depth back to zero, or -1 when the section
    // stays open past this line. Self-closing <template /> tags do not change the depth.
    private static int ScanTemplateTagDepth(string line, ref int depth)
    {
        foreach (Match match in TemplateSectionTagExpression.Matches(line))
        {
            bool isClosing = line[match.Index + 1] == '/';
            int tagClose = line.IndexOf('>', match.Index + match.Length);

            if (isClosing)
            {
                depth--;
                if (depth <= 0)
                {
                    return tagClose < 0 ? line.Length : tagClose + 1;
                }
            }
            else if (tagClose < 0 || line[tagClose - 1] != '/')
            {
                depth++;
            }
        }

        return -1;
    }

    // Classifies a top-level container tag header: '<' and '>' and '/' and '=' as operators, the tag
    // name as a markup node, and attributes (valueless ones such as 'scoped' included) as markup
    // attributes with their quoted values.
    private static void ClassifyTagHeader(
        string line,
        int lineNumber,
        int start,
        int end,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        int nameStart = start + 1;
        int nameEnd = nameStart;
        while (nameEnd < end && IsTagNameCharacter(line[nameEnd]))
        {
            nameEnd++;
        }

        AddSpan(
            lineNumber,
            nameStart,
            nameEnd - nameStart,
            ViuClassificationKind.MarkupNode,
            occupiedCharacters,
            spans);

        if (nameEnd < end)
        {
            string attributeText = line.Substring(nameEnd, end - nameEnd);
            foreach (Match match in TagHeaderAttributeExpression.Matches(attributeText))
            {
                Group attributeName = match.Groups["name"];
                AddSpan(
                    lineNumber,
                    nameEnd + attributeName.Index,
                    attributeName.Length,
                    ViuClassificationKind.MarkupAttribute,
                    occupiedCharacters,
                    spans);

                Group attributeValue = match.Groups["value"];
                if (attributeValue.Success)
                {
                    AddSpan(
                        lineNumber,
                        nameEnd + attributeValue.Index,
                        attributeValue.Length,
                        ViuClassificationKind.MarkupAttributeValue,
                        occupiedCharacters,
                        spans);
                }
            }
        }

        for (int characterIndex = start; characterIndex < end; characterIndex++)
        {
            if (line[characterIndex] is '<' or '>' or '/' or '=')
            {
                AddSpan(
                    lineNumber,
                    characterIndex,
                    1,
                    ViuClassificationKind.Operator,
                    occupiedCharacters,
                    spans);
            }
        }
    }

    // Classifies a raw-text section's closing tag ("</style>" or "</script>") as markup.
    private static void ClassifyClosingTag(
        string line,
        int lineNumber,
        int start,
        int length,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        AddSpan(lineNumber, start, 2, ViuClassificationKind.Operator, occupiedCharacters, spans);
        AddSpan(
            lineNumber,
            start + 2,
            length - 3,
            ViuClassificationKind.MarkupNode,
            occupiedCharacters,
            spans);
        AddSpan(
            lineNumber,
            start + length - 1,
            1,
            ViuClassificationKind.Operator,
            occupiedCharacters,
            spans);
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
            string attributeName = nameGroup.Value;
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                IsDirectiveName(attributeName)
                    ? ViuClassificationKind.Directive
                    : ViuClassificationKind.MarkupAttribute,
                occupiedCharacters,
                spans);

            Group valueGroup = match.Groups["value"];
            if (string.Equals(
                    attributeName.TrimStart('@', ':', '#'),
                    "class",
                    StringComparison.Ordinal))
            {
                ClassifyUtilityClassValue(
                    lineNumber,
                    line,
                    valueGroup,
                    occupiedCharacters,
                    spans);
            }
            else
            {
                AddSpan(
                    lineNumber,
                    valueGroup.Index,
                    valueGroup.Length,
                    ViuClassificationKind.MarkupAttributeValue,
                    occupiedCharacters,
                    spans);
            }
        }

        // Interpolations claim before valueless directives and tag names so their interiors color as
        // C# rather than as markup fragments.
        foreach (Match match in TemplateInterpolationExpression.Matches(line))
        {
            AddSpan(
                lineNumber,
                match.Index,
                2,
                ViuClassificationKind.InterpolationDelimiter,
                occupiedCharacters,
                spans);
            AddSpan(
                lineNumber,
                match.Index + match.Length - 2,
                2,
                ViuClassificationKind.InterpolationDelimiter,
                occupiedCharacters,
                spans);

            Group contentGroup = match.Groups["content"];
            if (contentGroup.Length > 0)
            {
                ClassifyCSharpTokens(
                    line,
                    lineNumber,
                    contentGroup.Index,
                    contentGroup.Length,
                    occupiedCharacters,
                    spans);
            }
        }

        // A valueless directive (v-else, v-once) has no '=' for the attribute pass to anchor on.
        ClassifyMatches(
            lineNumber,
            TemplateValuelessDirectiveExpression.Matches(line),
            ViuClassificationKind.Directive,
            occupiedCharacters,
            spans);

        foreach (Match match in TemplateTagExpression.Matches(line))
        {
            Group nameGroup = match.Groups["name"];
            string tagName = nameGroup.Value;
            // Viu components are named in PascalCase (or as dotted member expressions); a lowercase
            // tag is an HTML element or a lowercase built-in. The classifier is lexical — it has no
            // component registry to consult — so casing is the only signal available, and it is a
            // reliable one because name resolution is ordinal over the authored spelling ([CMP-6]).
            // Components therefore borrow the type category so they read as types.
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                char.IsAsciiLetterUpper(tagName[0]) || tagName.Contains('.')
                    ? ViuClassificationKind.Component
                    : ViuClassificationKind.MarkupNode,
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

    private static bool IsDirectiveName(string attributeName)
        => attributeName.Length > 0 &&
           (attributeName[0] is '@' or ':' or '#' ||
            attributeName.StartsWith("v-", StringComparison.Ordinal));

    // Splits a class attribute value into utility tokens: quotes keep the plain value category, each
    // leading "variant:" segment (colon included) is a utility variant, and the remainder — with
    // [...] arbitrary values never split on their inner colons — is the utility class. Purely
    // lexical: candidate validation stays in the language server (docs/DESIGN.md).
    private static void ClassifyUtilityClassValue(
        int lineNumber,
        string line,
        Group valueGroup,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        int valueStart = valueGroup.Index;
        int valueEnd = valueGroup.Index + valueGroup.Length;
        AddSpan(
            lineNumber,
            valueStart,
            1,
            ViuClassificationKind.MarkupAttributeValue,
            occupiedCharacters,
            spans);
        AddSpan(
            lineNumber,
            valueEnd - 1,
            1,
            ViuClassificationKind.MarkupAttributeValue,
            occupiedCharacters,
            spans);

        int position = valueStart + 1;
        int interiorEnd = valueEnd - 1;
        while (position < interiorEnd)
        {
            if (char.IsWhiteSpace(line[position]))
            {
                position++;
                continue;
            }

            int tokenStart = position;
            while (position < interiorEnd && !char.IsWhiteSpace(line[position]))
            {
                position++;
            }

            ClassifyUtilityToken(
                lineNumber,
                line,
                tokenStart,
                position,
                occupiedCharacters,
                spans);
        }
    }

    private static void ClassifyUtilityToken(
        int lineNumber,
        string line,
        int tokenStart,
        int tokenEnd,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        int bracketDepth = 0;
        int segmentStart = tokenStart;
        for (int characterIndex = tokenStart; characterIndex < tokenEnd; characterIndex++)
        {
            char character = line[characterIndex];
            if (character == '[')
            {
                bracketDepth++;
            }
            else if (character == ']')
            {
                bracketDepth--;
            }
            else if (character == ':' && bracketDepth == 0)
            {
                AddSpan(
                    lineNumber,
                    segmentStart,
                    characterIndex - segmentStart + 1,
                    ViuClassificationKind.UtilityVariant,
                    occupiedCharacters,
                    spans);
                segmentStart = characterIndex + 1;
            }
        }

        if (segmentStart < tokenEnd)
        {
            AddSpan(
                lineNumber,
                segmentStart,
                tokenEnd - segmentStart,
                ViuClassificationKind.UtilityClass,
                occupiedCharacters,
                spans);
        }
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

        ClassifyCSharpTokens(line, lineNumber, 0, line.Length, occupiedCharacters, spans);
    }

    // The C# token passes shared by the @script section and template interpolation interiors
    // (strings, keywords, methods, types, numbers, punctuation, operators — the comment state
    // machine deliberately excluded). The window bounds keep interpolation classification inside
    // the mustache delimiters.
    private static void ClassifyCSharpTokens(
        string line,
        int lineNumber,
        int windowStart,
        int windowLength,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        string window = windowStart == 0 && windowLength == line.Length
            ? line
            : line.Substring(windowStart, windowLength);

        ClassifyWindowMatches(
            lineNumber,
            windowStart,
            ScriptStringExpression.Matches(window),
            ViuClassificationKind.String,
            occupiedCharacters,
            spans);

        ClassifyWindowMatches(
            lineNumber,
            windowStart,
            ScriptKeywordExpression.Matches(window),
            ViuClassificationKind.Keyword,
            occupiedCharacters,
            spans);

        foreach (Match match in ScriptMethodExpression.Matches(window))
        {
            Group nameGroup = match.Groups["name"];
            AddSpan(
                lineNumber,
                windowStart + nameGroup.Index,
                nameGroup.Length,
                ViuClassificationKind.Method,
                occupiedCharacters,
                spans);
        }

        ClassifyWindowMatches(
            lineNumber,
            windowStart,
            ScriptTypeExpression.Matches(window),
            ViuClassificationKind.Type,
            occupiedCharacters,
            spans);

        ClassifyWindowMatches(
            lineNumber,
            windowStart,
            NumberExpression.Matches(window),
            ViuClassificationKind.Number,
            occupiedCharacters,
            spans);

        for (int characterIndex = windowStart; characterIndex < windowStart + windowLength; characterIndex++)
        {
            if ("{}[]();,.<>".IndexOf(line[characterIndex], StringComparison.Ordinal) >= 0)
            {
                AddSpan(
                    lineNumber,
                    characterIndex,
                    1,
                    ViuClassificationKind.Punctuation,
                    occupiedCharacters,
                    spans);
            }
        }

        for (int characterIndex = windowStart; characterIndex < windowStart + windowLength; characterIndex++)
        {
            if ("+-*/%=!&|?:".IndexOf(line[characterIndex], StringComparison.Ordinal) >= 0)
            {
                AddSpan(
                    lineNumber,
                    characterIndex,
                    1,
                    ViuClassificationKind.Operator,
                    occupiedCharacters,
                    spans);
            }
        }
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
            // Custom properties ("--name") are the theme's tokens; they borrow the type category so
            // they stand apart from ordinary declarations.
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                nameGroup.Value.StartsWith("--", StringComparison.Ordinal)
                    ? ViuClassificationKind.Type
                    : ViuClassificationKind.MarkupAttribute,
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

    private static void ClassifyWindowMatches(
        int lineNumber,
        int windowStart,
        MatchCollection matches,
        ViuClassificationKind classificationKind,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        foreach (Match match in matches)
        {
            AddSpan(
                lineNumber,
                windowStart + match.Index,
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

    private static bool IsTagNameCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

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
