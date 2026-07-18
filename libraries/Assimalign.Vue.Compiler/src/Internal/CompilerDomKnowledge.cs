using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The generator-host consumption of the shared DOM knowledge data: builds plain
/// <see cref="HashSet{T}"/> lookups from the linked <c>DomKnowledgeData</c> source (the
/// [V01.01.01.03] shared-source seam), exactly as the runtime <c>DomKnowledge</c> does over frozen
/// tables. netstandard2.0 has no <c>FrozenSet</c>/<c>SearchValues</c>, so a <see cref="HashSet{T}"/>
/// with the matching comparer is used — HTML tags are case-insensitive per WHATWG; SVG and MathML are
/// case-sensitive per their specs.
/// </summary>
internal static class CompilerDomKnowledge
{
    private static readonly HashSet<string> HtmlTagSet = Build(DomKnowledgeData.HtmlTags, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SvgTagSet = Build(DomKnowledgeData.SvgTags, StringComparer.Ordinal);
    private static readonly HashSet<string> MathTagSet = Build(DomKnowledgeData.MathTags, StringComparer.Ordinal);
    private static readonly HashSet<string> VoidTagSet = Build(DomKnowledgeData.VoidTags, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="tag"/> is a known HTML element (upstream <c>isHTMLTag</c>).</summary>
    /// <param name="tag">The tag name (matched case-insensitively).</param>
    public static bool IsHtmlTag(string tag) => HtmlTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a known SVG element (upstream <c>isSVGTag</c>).</summary>
    /// <param name="tag">The tag name (matched case-sensitively).</param>
    public static bool IsSvgTag(string tag) => SvgTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a known MathML element (upstream <c>isMathMLTag</c>).</summary>
    /// <param name="tag">The tag name (matched case-sensitively).</param>
    public static bool IsMathMLTag(string tag) => MathTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a void element (upstream <c>isVoidTag</c>, WHATWG).</summary>
    /// <param name="tag">The tag name (matched case-insensitively).</param>
    public static bool IsVoidTag(string tag) => VoidTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a native HTML/SVG/MathML element (upstream <c>isNativeTag</c>).</summary>
    /// <param name="tag">The tag name.</param>
    public static bool IsNativeTag(string tag) => IsHtmlTag(tag) || IsSvgTag(tag) || IsMathMLTag(tag);

    private static HashSet<string> Build(string list, StringComparer comparer)
    {
        var set = new HashSet<string>(comparer);
        foreach (var entry in list.Split(','))
        {
            set.Add(entry);
        }

        return set;
    }
}
