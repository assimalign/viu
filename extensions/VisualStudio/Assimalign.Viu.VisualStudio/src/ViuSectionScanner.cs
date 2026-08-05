using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The container-section state machine of the hybrid <c>.viu</c> format: it owns the patterns that
/// open and close a section and answers which section each line of a document belongs to.
/// </summary>
/// <remarks>
/// <para>
/// This is the single definition of "where does a section start and stop" in the extension.
/// <see cref="ViuLexicalClassifier"/> drives its own per-line classification from these primitives,
/// and <see cref="ViuAutoClosingLogic"/> drives auto-closing from <see cref="ScanLineSections"/>, so
/// neither carries its own copy of the container grammar ([V01.01.12.07.08]).
/// </para>
/// <para>
/// Attribution is per line, which is what both consumers need and what the format supports: a
/// section boundary is a line-anchored construct in every container form — the legacy
/// <c>@template {</c> header and its column-0 <c>}</c>, and the top-level <c>&lt;template&gt;</c> /
/// <c>&lt;style&gt;</c> / <c>&lt;script&gt;</c> tags the hybrid container introduced
/// ([V01.01.06.10]). A line that opens a section is attributed to that section, because content may
/// follow the opener on the same line; a line that closes one is attributed to it for the same
/// reason. The one exception mirrors the classifier: the column-0 <c>}</c> that ends a legacy
/// <c>@</c>-block is structure rather than content, so its line is attributed to no section.
/// </para>
/// <para>
/// Purely lexical and editor-free — no Visual Studio type appears here, which is what lets the same
/// source be compiled into the extension assembly and, through <c>&lt;Compile Include&gt;</c> links,
/// into a <c>dotnet test</c> project. The language surface is the .NET Framework one the extension
/// compiles against.
/// </para>
/// </remarks>
internal static class ViuSectionScanner
{
    /// <summary>The tag that closes a top-level <c>&lt;style&gt;</c> section.</summary>
    internal const string StyleClosingTag = "</style>";

    /// <summary>The tag that closes a top-level <c>&lt;script&gt;</c> section.</summary>
    internal const string ScriptClosingTag = "</script>";

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

    /// <summary>
    /// Matches a legacy <c>@template</c> / <c>@script</c> / <c>@style</c> block header, capturing the
    /// section name in <c>name</c> and its opening brace in <c>brace</c>.
    /// </summary>
    /// <param name="line">The line to match, without its line break.</param>
    /// <returns>The match; <see cref="Group.Success"/> is false when the line is not a header.</returns>
    internal static Match MatchSectionHeader(string line) => SectionHeaderExpression.Match(line);

    /// <summary>
    /// Matches a top-level <c>&lt;template&gt;</c> / <c>&lt;style&gt;</c> / <c>&lt;script&gt;</c>
    /// opening tag, capturing the tag name in <c>name</c>.
    /// </summary>
    /// <param name="line">The line to match, without its line break.</param>
    /// <returns>The match; <see cref="Group.Success"/> is false when the line opens no section.</returns>
    internal static Match MatchTagSectionOpen(string line) => TagSectionOpenExpression.Match(line);

    /// <summary>
    /// Maps a container name — from either header form — onto the section it opens.
    /// </summary>
    /// <param name="sectionName">The captured container name.</param>
    /// <returns>The section kind, or <see cref="ViuSectionKind.None"/> for an unknown name.</returns>
    internal static ViuSectionKind GetSectionKind(string sectionName) => sectionName switch
    {
        "template" => ViuSectionKind.Template,
        "script" => ViuSectionKind.Script,
        "style" => ViuSectionKind.Style,
        _ => ViuSectionKind.None,
    };

    /// <summary>
    /// Walks the <c>&lt;template&gt;</c>/<c>&lt;/template&gt;</c> boundaries on a line, updating the
    /// nesting depth.
    /// </summary>
    /// <param name="line">The line to walk.</param>
    /// <param name="depth">The nesting depth on entry, updated in place.</param>
    /// <returns>
    /// The exclusive end offset of the closer that brings the depth back to zero, or <c>-1</c> when
    /// the section stays open past this line. Self-closing <c>&lt;template /&gt;</c> tags do not
    /// change the depth, so a slot fragment such as <c>&lt;template #header&gt;</c> never ends the
    /// section.
    /// </returns>
    internal static int ScanTemplateTagDepth(string line, ref int depth)
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

    /// <summary>
    /// Attributes every line of a document to the container section that encloses it.
    /// </summary>
    /// <param name="lines">The document's lines, without their line breaks.</param>
    /// <returns>
    /// One <see cref="ViuSectionKind"/> per line, in document order. Lines outside every section —
    /// text between containers and the column-0 <c>}</c> that closes a legacy <c>@</c>-block — are
    /// <see cref="ViuSectionKind.None"/>.
    /// </returns>
    public static ViuSectionKind[] ScanLineSections(IReadOnlyList<string> lines)
    {
        ViuSectionKind[] lineSections = new ViuSectionKind[lines.Count];
        ViuSectionKind sectionKind = ViuSectionKind.None;
        bool isTagDelimitedSection = false;
        int templateTagDepth = 0;

        for (int lineNumber = 0; lineNumber < lines.Count; lineNumber++)
        {
            string line = lines[lineNumber];

            // Tag-delimited sections close at their matching end tag, never at an @-header or a
            // column-0 '}' — the closer alone decides where the section ends.
            if (isTagDelimitedSection)
            {
                lineSections[lineNumber] = sectionKind;
                if (ClosesTagDelimitedSection(line, sectionKind, 0, ref templateTagDepth))
                {
                    sectionKind = ViuSectionKind.None;
                    isTagDelimitedSection = false;
                }

                continue;
            }

            Match sectionMatch = MatchSectionHeader(line);
            if (sectionMatch.Success)
            {
                sectionKind = GetSectionKind(sectionMatch.Groups["name"].Value);
                lineSections[lineNumber] = sectionKind;
                continue;
            }

            if (sectionKind == ViuSectionKind.None)
            {
                Match tagMatch = MatchTagSectionOpen(line);
                if (tagMatch.Success)
                {
                    lineSections[lineNumber] = OpenTagDelimitedSection(
                        line,
                        tagMatch,
                        ref sectionKind,
                        ref isTagDelimitedSection,
                        ref templateTagDepth);
                    continue;
                }
            }
            else if (line.Length > 0 && line[0] == '}')
            {
                // Column 0 is structural in the hybrid format: this '}' closes the open @-block, and
                // is no more part of the section than the header that opened it.
                sectionKind = ViuSectionKind.None;
                lineSections[lineNumber] = ViuSectionKind.None;
                continue;
            }

            lineSections[lineNumber] = sectionKind;
        }

        return lineSections;
    }

    // Opens a tag-delimited section on its opening-tag line and returns the section that line's
    // content belongs to. A self-closing top-level tag carries no content, so no section opens; a
    // section whose closer sits on the same line opens and closes here.
    private static ViuSectionKind OpenTagDelimitedSection(
        string line,
        Match tagMatch,
        ref ViuSectionKind sectionKind,
        ref bool isTagDelimitedSection,
        ref int templateTagDepth)
    {
        Group nameGroup = tagMatch.Groups["name"];
        int headerClose = line.IndexOf('>', nameGroup.Index + nameGroup.Length);
        if (headerClose > 0 && line[headerClose - 1] == '/')
        {
            return ViuSectionKind.None;
        }

        ViuSectionKind openedKind = GetSectionKind(nameGroup.Value);
        sectionKind = openedKind;
        isTagDelimitedSection = true;
        templateTagDepth = 0;

        // A template's depth scan reads the whole line, opening tag included; a raw-text section's
        // closer search starts past its own header so "<style></style>" is recognized as one line.
        int searchStart = openedKind == ViuSectionKind.Template
            ? 0
            : headerClose < 0 ? line.Length : headerClose + 1;

        if (ClosesTagDelimitedSection(line, openedKind, searchStart, ref templateTagDepth))
        {
            sectionKind = ViuSectionKind.None;
            isTagDelimitedSection = false;
        }

        return openedKind;
    }

    private static bool ClosesTagDelimitedSection(
        string line,
        ViuSectionKind sectionKind,
        int searchStart,
        ref int templateTagDepth)
    {
        switch (sectionKind)
        {
            case ViuSectionKind.Template:
                return ScanTemplateTagDepth(line, ref templateTagDepth) >= 0;

            case ViuSectionKind.Style:
                return FindClosingTag(line, StyleClosingTag, searchStart) >= 0;

            case ViuSectionKind.Script:
                return FindClosingTag(line, ScriptClosingTag, searchStart) >= 0;

            default:
                return false;
        }
    }

    private static int FindClosingTag(string line, string closingTag, int searchStart) =>
        searchStart <= line.Length
            ? line.IndexOf(closingTag, searchStart, StringComparison.Ordinal)
            : -1;
}
