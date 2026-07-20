using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// Turns HackerNews comment/post HTML into safe, renderer-agnostic plain-text paragraphs. The sample
/// deliberately does <b>not</b> inject raw HTML (no <c>v-html</c>/<c>innerHTML</c>): that would be a
/// DOM-only escape hatch and an XSS surface, at odds with the SSR-ready "render through the node-ops
/// adapter only" boundary (#103). Tags are stripped and entities decoded so the text renders through
/// ordinary text vnodes on any renderer (browser and the in-memory test renderer alike). The tag
/// pattern is a <see cref="GeneratedRegexAttribute"/> so it is trimming/NativeAOT-safe (no runtime
/// regex compilation).
/// </summary>
internal static partial class HtmlText
{
    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagPattern();

    /// <summary>
    /// Splits <paramref name="html"/> into decoded, tag-free paragraphs (on <c>&lt;p&gt;</c>
    /// boundaries). Empty/blank input yields an empty list.
    /// </summary>
    /// <param name="html">The source HTML, or null.</param>
    /// <returns>The paragraphs, in order.</returns>
    public static IReadOnlyList<string> ToParagraphs(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<string>();
        }

        var withBreaks = html
            .Replace("<p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase);
        var stripped = TagPattern().Replace(withBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(stripped);

        var paragraphs = new List<string>();
        foreach (var part in decoded.Split('\n'))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                paragraphs.Add(trimmed);
            }
        }
        return paragraphs;
    }
}
