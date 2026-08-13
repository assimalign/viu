using System;
using System.Text;
using System.Text.Json.Nodes;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Serializes language-service Markdown into protocol <c>MarkupContent</c>, downgrading to
/// plaintext for clients that do not advertise Markdown support. The downgrade lives in the server
/// because the language service stays editor-neutral Markdown; format negotiation is a protocol
/// concern.
/// </summary>
internal static class LanguageServerMarkupContent
{
    /// <summary>Creates a <c>MarkupContent</c> object in the client's negotiated format.</summary>
    /// <param name="markdown">The language-service Markdown body.</param>
    /// <param name="supportsMarkdown">Whether the client advertised Markdown for this surface.</param>
    /// <returns>The serialized <c>MarkupContent</c>.</returns>
    internal static JsonObject Create(string markdown, bool supportsMarkdown)
        => new()
        {
            ["kind"] = supportsMarkdown ? "markdown" : "plaintext",
            ["value"] = supportsMarkdown ? markdown : ConvertToPlaintext(markdown),
        };

    /// <summary>
    /// Strips exactly the Markdown markers this codebase generates: fence delimiter lines (the CSS
    /// text between them is kept), bold markers, and inline code backticks. Newlines are preserved.
    /// </summary>
    /// <param name="markdown">The language-service Markdown body.</param>
    /// <returns>The plaintext rendering.</returns>
    internal static string ConvertToPlaintext(string markdown)
    {
        var builder = new StringBuilder(markdown.Length);
        var isFirstEmittedLine = true;
        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            if (!isFirstEmittedLine)
            {
                builder.Append('\n');
            }

            builder.Append(
                line
                    .Replace("**", string.Empty, StringComparison.Ordinal)
                    .Replace("`", string.Empty, StringComparison.Ordinal));
            isFirstEmittedLine = false;
        }

        return builder.ToString();
    }
}
