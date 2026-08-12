using System;
using System.Collections.Generic;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Turns a hover's Markdown into the classified lines a tooltip is drawn from.
/// </summary>
/// <remarks>
/// <para>
/// The server sends a fenced declaration and prose about it. Handing that Markdown to the host draws
/// the fence as a document artifact, complete with a copy button, and nothing in a tooltip is meant
/// to be lifted out. Rendering it here instead both removes the decoration and colors the
/// declaration, because the fence's language says which grammar to color it with.
/// </para>
/// <para>
/// The colors come from <see cref="ViuLexicalClassifier"/> — the same pass that colors the document
/// behind the tooltip — so a signature reads in the tooltip exactly as it reads in the file. The
/// classifier answers whole documents, because a line's meaning depends on the container section
/// above it, so a declaration is classified by putting it in the section its language belongs to.
/// </para>
/// <para>
/// Editor-free, so what a tooltip is made of is unit-testable and the Visual Studio adapter that
/// draws it holds no rule of its own.
/// </para>
/// </remarks>
internal static class ViuHoverPresentation
{
    private const string FenceMarker = "```";

    /// <summary>Builds the classified lines for one hover body.</summary>
    /// <param name="markdown">The hover Markdown, or <see langword="null"/>.</param>
    /// <returns>One entry per rendered line, each a list of runs; empty for nothing to show.</returns>
    public static IReadOnlyList<IReadOnlyList<ViuHoverRun>> Create(string? markdown)
    {
        var lines = new List<IReadOnlyList<ViuHoverRun>>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return lines;
        }

        string? fenceLanguage = null;
        var fenced = new List<string>();
        foreach (string rawLine in markdown!.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.TrimStart().StartsWith(FenceMarker, StringComparison.Ordinal))
            {
                if (fenceLanguage is null)
                {
                    fenceLanguage = line.TrimStart().Substring(FenceMarker.Length).Trim();
                    continue;
                }

                AppendDeclaration(lines, fenceLanguage, fenced);
                fenced.Clear();
                fenceLanguage = null;
                continue;
            }

            if (fenceLanguage is not null)
            {
                fenced.Add(line);
                continue;
            }

            if (line.Length > 0)
            {
                lines.Add(CreateProseRuns(line));
            }
        }

        // A fence the server left open still describes a declaration; dropping it would lose the
        // whole signature over a missing marker.
        if (fenceLanguage is not null && fenced.Count > 0)
        {
            AppendDeclaration(lines, fenceLanguage, fenced);
        }

        return lines;
    }

    private static void AppendDeclaration(
        List<IReadOnlyList<ViuHoverRun>> lines,
        string language,
        IReadOnlyList<string> declarationLines)
    {
        if (declarationLines.Count == 0)
        {
            return;
        }

        // The classifier reads whole documents because a container section decides how its lines are
        // colored, so the declaration is placed in the section its language belongs to and the
        // wrapper lines are dropped again.
        bool isStyle = string.Equals(language, "css", StringComparison.OrdinalIgnoreCase);
        var document = new List<string>(declarationLines.Count + 2)
        {
            isStyle ? "<style>" : "@script {",
        };
        document.AddRange(declarationLines);
        document.Add(isStyle ? "</style>" : "}");

        IReadOnlyList<ViuLexicalSpan> spans = ViuLexicalClassifier.Classify(document);
        var spansByLine = new Dictionary<int, List<ViuLexicalSpan>>();
        foreach (ViuLexicalSpan span in spans)
        {
            if (!spansByLine.TryGetValue(span.LineNumber, out List<ViuLexicalSpan>? lineSpans))
            {
                lineSpans = [];
                spansByLine.Add(span.LineNumber, lineSpans);
            }

            lineSpans.Add(span);
        }

        for (var index = 0; index < declarationLines.Count; index++)
        {
            spansByLine.TryGetValue(index + 1, out List<ViuLexicalSpan>? lineSpans);
            lines.Add(CreateDeclarationRuns(declarationLines[index], lineSpans));
        }
    }

    private static IReadOnlyList<ViuHoverRun> CreateDeclarationRuns(
        string line,
        List<ViuLexicalSpan>? lineSpans)
    {
        var runs = new List<ViuHoverRun>();
        if (lineSpans is null || lineSpans.Count == 0)
        {
            runs.Add(new ViuHoverRun(ViuClassificationTypeNames.Identifier, line));
            return runs;
        }

        lineSpans.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        var position = 0;
        foreach (ViuLexicalSpan span in lineSpans)
        {
            if (span.Start < position || span.Start + span.Length > line.Length)
            {
                continue;
            }

            if (span.Start > position)
            {
                runs.Add(new ViuHoverRun(
                    ViuClassificationTypeNames.Identifier,
                    line.Substring(position, span.Start - position)));
            }

            runs.Add(new ViuHoverRun(
                ViuClassificationTypeNames.GetClassificationTypeName(span.ClassificationKind),
                line.Substring(span.Start, span.Length)));
            position = span.Start + span.Length;
        }

        if (position < line.Length)
        {
            runs.Add(new ViuHoverRun(
                ViuClassificationTypeNames.Identifier,
                line.Substring(position)));
        }

        return runs;
    }

    // Prose is body text. Its inline code spans mark names rather than offering code, so they carry
    // the identifier classification and their backticks are not shown.
    private static IReadOnlyList<ViuHoverRun> CreateProseRuns(string line)
    {
        var runs = new List<ViuHoverRun>();
        var position = 0;
        while (position < line.Length)
        {
            int open = line.IndexOf('`', position);
            if (open < 0)
            {
                break;
            }

            int close = line.IndexOf('`', open + 1);
            if (close < 0)
            {
                break;
            }

            if (open > position)
            {
                runs.Add(new ViuHoverRun(
                    ViuClassificationTypeNames.Text,
                    line.Substring(position, open - position)));
            }

            if (close > open + 1)
            {
                runs.Add(new ViuHoverRun(
                    ViuClassificationTypeNames.Identifier,
                    line.Substring(open + 1, close - open - 1)));
            }

            position = close + 1;
        }

        if (position < line.Length)
        {
            runs.Add(new ViuHoverRun(
                ViuClassificationTypeNames.Text,
                line.Substring(position)));
        }

        return runs;
    }
}
