using System;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.Sdk.Browser.Tasks;

/// <summary>
/// Provides the pure host-page transformation used by <see cref="ViuInjectCssBundleLink"/>.
/// </summary>
internal static class CssBundleLinkInjector
{
    /// <summary>
    /// Splices a <c>&lt;link rel="stylesheet" href="{href}"&gt;</c> immediately before the first
    /// <c>&lt;/head&gt;</c> (case-insensitive), preserving the document's newline style and the close tag's
    /// indentation. Returns the input unchanged (with <paramref name="injected"/> <see langword="false"/>) when
    /// the bundle is already referenced or no <c>&lt;/head&gt;</c> is present.
    /// </summary>
    internal static string InjectStylesheetLink(string html, string href, string bundleFileName, out bool injected)
    {
        injected = false;
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        // Idempotent: only an exact href path-segment reference suppresses a second link. A bare mention
        // in prose, a comment, or a suffix-colliding bundle name must not suppress injection.
        if (ReferencesInHref(html, bundleFileName) || ReferencesInHref(html, href))
        {
            return html;
        }

        var closeHead = Regex.Match(html, "</head\\s*>", RegexOptions.IgnoreCase);
        if (!closeHead.Success)
        {
            return html;
        }

        var newline = html.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
        var indent = LeadingWhitespaceOfLine(html, closeHead.Index);
        var link = "<link rel=\"stylesheet\" href=\"" + href + "\" />";
        var insertion = link + newline + indent;
        injected = true;
        return html.Substring(0, closeHead.Index) + insertion + html.Substring(closeHead.Index);
    }

    private static bool ReferencesInHref(string html, string name)
    {
        return !string.IsNullOrEmpty(name)
            && Regex.IsMatch(
                html,
                "href\\s*=\\s*(?<quote>[\"'])(?:[^\"'?#]*[/\\\\])?" +
                Regex.Escape(name) +
                "(?=[?#]|\\k<quote>)",
                RegexOptions.IgnoreCase);
    }

    private static string LeadingWhitespaceOfLine(string html, int index)
    {
        var lineStart = html.LastIndexOf('\n', index > 0 ? index - 1 : 0);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var run = index - lineStart;
        for (var offset = 0; offset < run; offset++)
        {
            var character = html[lineStart + offset];
            if (character != ' ' && character != '\t')
            {
                return string.Empty;
            }
        }

        return html.Substring(lineStart, run);
    }
}
