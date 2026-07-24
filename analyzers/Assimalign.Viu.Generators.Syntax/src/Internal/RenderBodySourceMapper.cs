using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// Injects C# <c>#line</c> span directives into a compiled <c>@template</c> render body so a C# compile
/// error inside an emitted template expression resolves to the offending <c>.viu</c> template line and
/// column ([V01.01.05.08]) — the render-body analogue of the <c>@script</c> merge's <c>#line</c> map in
/// <see cref="SingleFileComponentSourceEmitter"/>. Each dynamic expression's
/// <see cref="RenderSourceMapping"/> (its position in the emitted body plus the template location it came
/// from) becomes a directive that aligns the emitted text to its template span through
/// <see cref="SingleFileComponentDiagnostics.ComposeToFilePosition"/> — the same block-to-file arithmetic
/// the <c>@template</c>/<c>@script</c> diagnostic paths use, so the emitted map and the reported
/// diagnostics agree exactly.
/// </summary>
/// <remarks>
/// A C# <c>#line (startLine,startColumn)-(endLine,endColumn) charOffset "file"</c> directive maps the text
/// starting at physical column <c>charOffset</c> on the following line linearly onto the target span, and
/// that mapping stays in effect across the rest of the physical line (<c>endColumn</c> bounds the nominal
/// span but does not stop the linear extrapolation). Only one directive can lead a physical line, so every
/// render-body line that bears expressions is bracketed individually — anchored to its FIRST (leftmost)
/// expression and closed with <c>#line default</c> so the mapping never bleeds onto the next line.
/// Scaffolding lines, and any additional expressions sharing a physical line, resolve to the generated file
/// (the standard generated-code fallback: a scaffold error is a generator concern, not a template one). The
/// codegen already splits multi-entry props and long child arrays across lines, so multiple dynamic
/// expressions on one physical line are the uncommon adjacent-interpolation case, where the shared anchor
/// still lands them on the correct template line. Emitted positions are one-based per the <c>#line</c>
/// convention.
/// </remarks>
internal static class RenderBodySourceMapper
{
    /// <summary>
    /// Returns <paramref name="renderBody"/> with <c>#line</c> span directives injected for every mapped
    /// expression line, or the body unchanged when there is nothing to map.
    /// </summary>
    /// <param name="renderBody">The emitted render-method body (LF-terminated lines).</param>
    /// <param name="mappings">The render source map from the template compiler.</param>
    /// <param name="blockContentStart">The <c>.viu</c> file position where the <c>@template</c> block content begins.</param>
    /// <param name="filePath">The originating <c>.viu</c> file path (the <c>#line</c> target).</param>
    /// <returns>The render body with source-mapping directives.</returns>
    public static string Inject(
        string renderBody,
        SyntaxList<RenderSourceMapping> mappings,
        Position blockContentStart,
        string filePath)
    {
        if (mappings.Count == 0)
        {
            return renderBody;
        }

        // Anchor each generated line to its leftmost mapping: only one #line directive can lead a line, so
        // the first expression is the one whose column the directive aligns.
        var anchors = new Dictionary<int, RenderSourceMapping>();
        foreach (var mapping in mappings)
        {
            if (!anchors.TryGetValue(mapping.GeneratedLine, out var existing) ||
                mapping.GeneratedColumn < existing.GeneratedColumn)
            {
                anchors[mapping.GeneratedLine] = mapping;
            }
        }

        // Split on LF: the emitter writes LF newlines and ends with a trailing LF, so the final fragment is
        // the empty string, and rejoining fragment-by-fragment reproduces the body exactly.
        var lines = renderBody.Split('\n');
        var builder = new StringBuilder(renderBody.Length + (anchors.Count * 48));
        for (var index = 0; index < lines.Length; index++)
        {
            var isLastFragment = index == lines.Length - 1;
            if (anchors.TryGetValue(index, out var anchor))
            {
                AppendDirective(builder, anchor, blockContentStart, filePath);
                builder.Append(lines[index]).Append('\n');
                builder.Append("#line default\n");
            }
            else
            {
                builder.Append(lines[index]);
                if (!isLastFragment)
                {
                    builder.Append('\n');
                }
            }
        }

        return builder.ToString();
    }

    private static void AppendDirective(
        StringBuilder builder,
        RenderSourceMapping anchor,
        Position blockContentStart,
        string filePath)
    {
        var (startLine, startColumn) = SingleFileComponentDiagnostics.ComposeToFilePosition(
            blockContentStart, anchor.TemplateLocation.Start);
        var (endLine, endColumn) = SingleFileComponentDiagnostics.ComposeToFilePosition(
            blockContentStart, anchor.TemplateLocation.End);

        // The line-span directive form verified against the real compiler: aligns physical column
        // charOffset on the next line to the (startLine, startColumn) template coordinate.
        builder.Append("#line (")
            .Append(startLine.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(startColumn.ToString(CultureInfo.InvariantCulture)).Append(")-(")
            .Append(endLine.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(endColumn.ToString(CultureInfo.InvariantCulture)).Append(") ")
            .Append(anchor.GeneratedColumn.ToString(CultureInfo.InvariantCulture)).Append(" \"")
            .Append(filePath).Append("\"\n");
    }
}
