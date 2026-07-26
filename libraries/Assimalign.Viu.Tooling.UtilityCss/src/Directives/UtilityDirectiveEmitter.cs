using System;
using System.Text;

namespace Assimalign.Viu.Tooling.UtilityCss;

/// <summary>
/// Produces a deterministic CSS projection of parsed utility directives.
/// </summary>
/// <remarks>
/// This emitter preserves directive semantics but deliberately does not expand <c>@apply</c>,
/// resolve variants, or inline references. Those operations require a project registry and a
/// host-supplied reference graph.
/// </remarks>
public static class UtilityDirectiveEmitter
{
    /// <summary>
    /// Emits valid supported directives in source order with normalized line endings and layout.
    /// </summary>
    /// <param name="directives">The immutable parsed directive sequence.</param>
    /// <returns>The canonical directive CSS projection.</returns>
    public static string Emit(UtilityCollection<UtilityDirective> directives)
    {
        var builder = new StringBuilder();
        foreach (var directive in directives)
        {
            if (!directive.IsValid)
            {
                continue;
            }

            if (builder.Length != 0)
            {
                builder.Append('\n');
            }

            builder.Append('@');
            builder.Append(GetDirectiveName(directive.Kind));
            if (directive.Parameters.Length != 0)
            {
                builder.Append(' ');
                builder.Append(NormalizeLineEndings(directive.Parameters).Trim());
            }

            if (!directive.HasBlock)
            {
                builder.Append(';');
                continue;
            }

            builder.Append(" {\n");
            AppendBody(
                builder,
                directive.Body ?? string.Empty);
            builder.Append('}');
        }

        return builder.ToString();
    }

    private static string GetDirectiveName(UtilityDirectiveKind kind) =>
        kind switch
        {
            UtilityDirectiveKind.Utility => "utility",
            UtilityDirectiveKind.CustomVariant => "custom-variant",
            UtilityDirectiveKind.Variant => "variant",
            UtilityDirectiveKind.Apply => "apply",
            UtilityDirectiveKind.Reference => "reference",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown utility directive kind."),
        };

    private static void AppendBody(
        StringBuilder builder,
        string body)
    {
        var normalized = NormalizeLineEndings(body);
        var lines = normalized.Split('\n');
        var first = 0;
        while (first < lines.Length &&
               string.IsNullOrWhiteSpace(lines[first]))
        {
            first++;
        }

        var last = lines.Length - 1;
        while (last >= first &&
               string.IsNullOrWhiteSpace(lines[last]))
        {
            last--;
        }

        if (first > last)
        {
            return;
        }

        var indentation = FindCommonIndentation(
            lines,
            first,
            last);
        for (var index = first; index <= last; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                builder.Append('\n');
                continue;
            }

            builder.Append("  ");
            var remove = Math.Min(
                indentation,
                CountLeadingWhitespace(line));
            var lineEnd = line.Length;
            while (lineEnd > remove &&
                   (line[lineEnd - 1] == ' ' ||
                    line[lineEnd - 1] == '\t'))
            {
                lineEnd--;
            }

            builder.Append(
                line,
                remove,
                lineEnd - remove);
            builder.Append('\n');
        }
    }

    private static int FindCommonIndentation(
        string[] lines,
        int first,
        int last)
    {
        var result = int.MaxValue;
        for (var index = first; index <= last; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            result = Math.Min(
                result,
                CountLeadingWhitespace(lines[index]));
        }

        return result == int.MaxValue
            ? 0
            : result;
    }

    private static int CountLeadingWhitespace(string value)
    {
        var index = 0;
        while (index < value.Length &&
               (value[index] == ' ' || value[index] == '\t'))
        {
            index++;
        }

        return index;
    }

    private static string NormalizeLineEndings(string value) =>
        value
            .Replace(
                "\r\n",
                "\n")
            .Replace(
                '\r',
                '\n');
}
