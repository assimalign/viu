using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Lexes a Viu single-file component into <see cref="ViuLexicalSpan"/> values.
/// </summary>
/// <remarks>
/// <para>
/// Purely lexical and editor-free: it consults no component registry, no semantic model, and no
/// Visual Studio type. That independence is what lets the same lexer be compiled into the extension
/// assembly and, through <c>&lt;Compile Include&gt;</c> links, into a <c>dotnet test</c> project.
/// </para>
/// <para>
/// Classification is whole-document by construction — the container sections above a line decide how
/// that line is colored — so callers lex a document once per snapshot and answer range requests from
/// the result.
/// </para>
/// <para>
/// The language surface here is deliberately the .NET Framework one this extension compiles against:
/// no <c>Array.Fill</c>, no <c>char.IsAscii*</c>, no <c>string.Contains(char)</c>. The helpers at the
/// bottom of this file stand in for them.
/// </para>
/// </remarks>
internal static class ViuLexicalClassifier
{
    // The container grammar itself - which line opens a section, which line closes it, and what a
    // container name means - lives in ViuSectionScanner, so this classifier and the auto-closing
    // decisions share one definition of the hybrid .viu sections ([V01.01.06.10]) instead of each
    // carrying a copy.
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

    private static readonly Regex ScriptIdentifierExpression = new(
        @"\b[A-Za-z_][A-Za-z0-9_]*",
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

    /// <summary>
    /// Lexes a whole document.
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <returns>
    /// The classified spans, in the order the passes claimed them. Spans never overlap: the first
    /// pass to claim a character owns it, which is how a more specific rule wins over a general one.
    /// </returns>
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
                                ViuSectionScanner.StyleClosingTag,
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
                                ViuSectionScanner.ScriptClosingTag,
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

            Match sectionMatch = ViuSectionScanner.MatchSectionHeader(line);

            if (sectionMatch.Success)
            {
                Group nameGroup = sectionMatch.Groups["name"];
                sectionKind = ViuSectionScanner.GetSectionKind(nameGroup.Value);

                // The legacy @template/@style headers name the same containers their tag-delimited
                // successors do, so they carry the framework-tag color. @script is the C# container's
                // own header and keeps the keyword classification, which puts it in the same color as
                // the C# it introduces.
                AddSpan(
                    lineNumber,
                    nameGroup.Index - 1,
                    nameGroup.Length + 1,
                    sectionKind == ViuSectionKind.Script
                        ? ViuClassificationKind.Keyword
                        : ViuClassificationKind.FrameworkTag,
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
                Match tagMatch = ViuSectionScanner.MatchTagSectionOpen(line);
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

        switch (ViuSectionScanner.GetSectionKind(nameGroup.Value))
        {
            case ViuSectionKind.Template:
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

            case ViuSectionKind.Style:
                sectionKind = ViuSectionKind.Style;
                isTagDelimitedSection = true;
                if (ClassifyTagDelimitedRawLine(
                        line,
                        lineNumber,
                        ViuSectionScanner.StyleClosingTag,
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

            case ViuSectionKind.Script:
                // The container parser rejects a top-level <script> tag (VIU1017); the lexer still
                // colors it as a script section so the misplaced code stays readable while the
                // diagnostic points at the fix.
                sectionKind = ViuSectionKind.Script;
                isTagDelimitedSection = true;
                if (ClassifyTagDelimitedRawLine(
                        line,
                        lineNumber,
                        ViuSectionScanner.ScriptClosingTag,
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
        int closerEnd = ViuSectionScanner.ScanTemplateTagDepth(line, ref templateTagDepth);
        if (closerEnd >= 0 && closerEnd < line.Length)
        {
            // Occupy everything past the true closer without emitting spans; whatever follows the
            // section is top-level text this line's template rules must not color.
            Occupy(occupiedCharacters, closerEnd, line.Length - closerEnd);
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
                Occupy(occupiedCharacters, afterCloser, line.Length - afterCloser);
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

    // Classifies a top-level container tag header: the tag punctuation as delimiters, the tag name as
    // the framework tag it always is at this position, and attributes (valueless ones such as
    // 'scoped' included) as markup attributes with their quoted values.
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
            GetTagNameClassification(line.Substring(nameStart, nameEnd - nameStart)),
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
                    ViuClassificationKind.Delimiter,
                    occupiedCharacters,
                    spans);
            }
        }
    }

    // Classifies a raw-text section's closing tag ("</style>" or "</script>").
    private static void ClassifyClosingTag(
        string line,
        int lineNumber,
        int start,
        int length,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        AddSpan(lineNumber, start, 2, ViuClassificationKind.Delimiter, occupiedCharacters, spans);
        AddSpan(
            lineNumber,
            start + 2,
            length - 3,
            ViuClassificationKind.FrameworkTag,
            occupiedCharacters,
            spans);
        AddSpan(
            lineNumber,
            start + length - 1,
            1,
            ViuClassificationKind.Delimiter,
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
            bool isDirective = IsDirectiveName(attributeName);
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                isDirective
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
            else if (isDirective)
            {
                ClassifyBindingExpressionValue(
                    lineNumber,
                    line,
                    valueGroup,
                    IsEventHandlerDirectiveName(attributeName),
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
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                GetTagNameClassification(nameGroup.Value),
                occupiedCharacters,
                spans);
        }

        ClassifyCharacters(
            line,
            lineNumber,
            "<>/=",
            ViuClassificationKind.Delimiter,
            occupiedCharacters,
            spans);
    }

    /// <summary>
    /// Classifies a tag name by what Viu makes of it at that position.
    /// </summary>
    /// <remarks>
    /// Three outcomes. <c>template</c>, <c>slot</c>, <c>style</c>, and <c>script</c> are the container
    /// and framework tags Viu itself defines, so they are colored as framework tags wherever they
    /// appear — a nested <c>&lt;template #header&gt;</c> slot fragment included. A PascalCase or
    /// dotted name is a component: casing is the only signal a purely lexical classifier has, and it
    /// is a reliable one because name resolution is ordinal over the authored spelling
    /// (<c>[CMP-6]</c>). Everything else is an HTML element.
    /// </remarks>
    private static ViuClassificationKind GetTagNameClassification(string tagName)
    {
        if (tagName.Length == 0)
        {
            return ViuClassificationKind.MarkupNode;
        }

        if (IsFrameworkTagName(tagName))
        {
            return ViuClassificationKind.FrameworkTag;
        }

        return tagName[0] is >= 'A' and <= 'Z' || tagName.IndexOf('.') >= 0
            ? ViuClassificationKind.Component
            : ViuClassificationKind.MarkupNode;
    }

    private static bool IsFrameworkTagName(string tagName) =>
        tagName is "template" or "slot" or "style" or "script";

    private static bool IsDirectiveName(string attributeName)
        => attributeName.Length > 0 &&
           (attributeName[0] is '@' or ':' or '#' ||
            attributeName.StartsWith("v-", StringComparison.Ordinal));

    /// <summary>
    /// Determines whether a directive attribute's value is an event-handler expression.
    /// </summary>
    /// <remarks>
    /// The check is exact rather than a <c>v-on</c> prefix test, because <c>v-once</c> shares that
    /// prefix and is not a handler binding.
    /// </remarks>
    private static bool IsEventHandlerDirectiveName(string attributeName) =>
        attributeName.Length > 0 &&
        (attributeName[0] == '@' ||
         string.Equals(attributeName, "v-on", StringComparison.Ordinal) ||
         attributeName.StartsWith("v-on:", StringComparison.Ordinal));

    /// <summary>
    /// Classifies a directive's quoted value: the quotes stay attribute value, the interior runs the
    /// C# token passes.
    /// </summary>
    /// <remarks>
    /// A binding value is C# source, so it colors as C# — the same passes the <c>@script</c> block and
    /// interpolation interiors use. Only <c>class</c> values are exempt; they are handled by
    /// <see cref="ClassifyUtilityClassValue"/>.
    /// </remarks>
    private static void ClassifyBindingExpressionValue(
        int lineNumber,
        string line,
        Group valueGroup,
        bool isEventHandler,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans)
    {
        int valueStart = valueGroup.Index;
        int valueEnd = valueStart + valueGroup.Length;

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

        int interiorLength = valueGroup.Length - 2;
        if (interiorLength > 0)
        {
            ClassifyCSharpTokens(
                line,
                lineNumber,
                valueStart + 1,
                interiorLength,
                occupiedCharacters,
                spans,
                isEventHandler
                    ? ViuClassificationKind.Method
                    : ViuClassificationKind.Identifier);
        }
    }

    // Splits a class attribute value into utility tokens: quotes keep the plain value category, each
    // leading "variant:" segment (colon included) is a utility variant, and the remainder — with
    // [...] arbitrary values never split on their inner colons — is the utility class. Purely
    // lexical: candidate validation stays in the language server (docs/DESIGN.md). All three
    // categories share one classification type, so a class attribute reads as a single value; the
    // kinds stay distinct because the language server and the Visual Studio Code grammar act on the
    // distinction.
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

    /// <summary>
    /// The C# token passes, shared by the <c>@script</c> section, template interpolation interiors,
    /// and binding-expression interiors (strings, keywords, methods, types, numbers, punctuation,
    /// operators — the comment state machine deliberately excluded).
    /// </summary>
    /// <param name="line">The line being classified.</param>
    /// <param name="lineNumber">Zero-based line number.</param>
    /// <param name="windowStart">Offset of the first character the passes may claim.</param>
    /// <param name="windowLength">Number of characters the passes may claim.</param>
    /// <param name="occupiedCharacters">Per-character claim map for the line.</param>
    /// <param name="spans">Accumulator the passes append to.</param>
    /// <param name="bareIdentifierKind">
    /// What an identifier that no earlier pass claimed should become, or <see langword="null"/> to
    /// leave bare identifiers to the type pass.
    /// </param>
    /// <remarks>
    /// <para>
    /// The window bounds keep interpolation and binding classification inside their delimiters.
    /// </para>
    /// <para>
    /// The method-position rule has two halves. Call syntax — an identifier immediately followed by
    /// <c>(</c> — is a method wherever a C# pass runs, the <c>@script</c> block included. In an
    /// event-handler value the handler slot itself is a method position too, so a bare identifier
    /// there is a method even without parentheses: <c>@click="Increment"</c> names a method exactly as
    /// <c>@click="Increment()"</c> does. An identifier followed by <c>.</c> is a receiver rather than
    /// the handler, so <c>@click="ViewModel.Increment"</c> still colors its two halves apart.
    /// </para>
    /// <para>
    /// A plain binding (<c>:value="Count"</c>, <c>v-if="Visible"</c>) names component state, so its
    /// bare identifiers stay identifiers. That is why <paramref name="bareIdentifierKind"/> exists at
    /// all: without it the PascalCase-is-a-type heuristic below would color every bound property as a
    /// type. The heuristic still runs unchanged in the <c>@script</c> block and in interpolation
    /// interiors, which are general C# where a PascalCase name really is usually a type; a binding
    /// value is the one position where the leading name is a member by construction.
    /// </para>
    /// </remarks>
    private static void ClassifyCSharpTokens(
        string line,
        int lineNumber,
        int windowStart,
        int windowLength,
        bool[] occupiedCharacters,
        List<ViuLexicalSpan> spans,
        ViuClassificationKind? bareIdentifierKind = null)
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

        if (bareIdentifierKind is { } identifierKind)
        {
            foreach (Match match in ScriptIdentifierExpression.Matches(window))
            {
                int afterMatch = match.Index + match.Length;
                if (afterMatch < window.Length && window[afterMatch] == '.')
                {
                    // A receiver, not the bound member: leave it to the type pass.
                    continue;
                }

                AddSpan(
                    lineNumber,
                    windowStart + match.Index,
                    match.Length,
                    identifierKind,
                    occupiedCharacters,
                    spans);
            }
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
            if ("{}[]();,.<>".IndexOf(line[characterIndex]) >= 0)
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
            if ("+-*/%=!&|?:".IndexOf(line[characterIndex]) >= 0)
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
            // Custom properties ("--name") are the theme's tokens and carry their own classification
            // so they stand apart from ordinary declarations, which share the attribute color.
            AddSpan(
                lineNumber,
                nameGroup.Index,
                nameGroup.Length,
                nameGroup.Value.StartsWith("--", StringComparison.Ordinal)
                    ? ViuClassificationKind.StyleCustomProperty
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
                ViuClassificationKind.StyleSelector,
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
            if (characters.IndexOf(line[characterIndex]) >= 0)
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
        => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';

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
        Occupy(occupiedCharacters, start, length);
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

    private static void Occupy(bool[] occupiedCharacters, int start, int length)
    {
        for (int characterIndex = start; characterIndex < start + length; characterIndex++)
        {
            occupiedCharacters[characterIndex] = true;
        }
    }
}
