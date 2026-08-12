using System.Text;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Renders a completion snippet as the plain text a client without snippet support should insert.
/// The downgrade lives in the server because the language service stays editor-neutral; which
/// insertion format a client can expand is a protocol negotiation.
/// </summary>
/// <remarks>
/// Placeholders name where a caret would go, and a client that cannot place carets has no use for
/// them: dropping them leaves the surrounding literal text, so <c>&lt;template$1&gt;$0&lt;/template&gt;</c>
/// inserts as <c>&lt;template&gt;&lt;/template&gt;</c> rather than showing the author the markers.
/// A tabstop with a default value keeps the default, which is the text the author would have accepted
/// anyway. An escaped <c>\$</c> is a literal dollar sign the snippet grammar was hiding — the
/// <c>$event</c> lambda depends on it — and unescapes here.
/// </remarks>
internal static class LanguageServerSnippetText
{
    /// <summary>Converts snippet-format insert text to its plain-text rendering.</summary>
    /// <param name="snippet">The insert text in the protocol's snippet format.</param>
    /// <returns>The text to insert verbatim.</returns>
    internal static string ToPlainText(string snippet)
    {
        var builder = new StringBuilder(snippet.Length);
        var index = 0;
        while (index < snippet.Length)
        {
            var character = snippet[index];
            if (character == '\\' && index + 1 < snippet.Length)
            {
                builder.Append(snippet[index + 1]);
                index += 2;
                continue;
            }

            if (character != '$')
            {
                builder.Append(character);
                index++;
                continue;
            }

            if (TrySkipTabstop(snippet, index, out var afterTabstop))
            {
                index = afterTabstop;
                continue;
            }

            if (TryReadChoiceOrPlaceholder(snippet, index, out var defaultValue, out var afterBrace))
            {
                builder.Append(defaultValue);
                index = afterBrace;
                continue;
            }

            // A lone '$' the grammar does not continue is literal text.
            builder.Append(character);
            index++;
        }

        return builder.ToString();
    }

    // "$0", "$1", … — a bare tabstop contributes nothing to the inserted text.
    private static bool TrySkipTabstop(string snippet, int start, out int afterTabstop)
    {
        var index = start + 1;
        while (index < snippet.Length && char.IsAsciiDigit(snippet[index]))
        {
            index++;
        }

        afterTabstop = index;
        return index > start + 1;
    }

    // "${1}" and "${1:default}" — the default is the text the author would have accepted.
    private static bool TryReadChoiceOrPlaceholder(
        string snippet,
        int start,
        out string defaultValue,
        out int afterBrace)
    {
        defaultValue = string.Empty;
        afterBrace = start;
        if (start + 1 >= snippet.Length || snippet[start + 1] != '{')
        {
            return false;
        }

        var index = start + 2;
        while (index < snippet.Length && char.IsAsciiDigit(snippet[index]))
        {
            index++;
        }

        if (index == start + 2 || index >= snippet.Length)
        {
            return false;
        }

        if (snippet[index] == '}')
        {
            afterBrace = index + 1;
            return true;
        }

        if (snippet[index] != ':')
        {
            return false;
        }

        var close = snippet.IndexOf('}', index);
        if (close < 0)
        {
            return false;
        }

        defaultValue = ToPlainText(snippet.Substring(index + 1, close - index - 1));
        afterBrace = close + 1;
        return true;
    }
}
