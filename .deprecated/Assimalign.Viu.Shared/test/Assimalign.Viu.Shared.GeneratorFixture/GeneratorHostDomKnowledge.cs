using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Shared;

/// <summary>
/// The generator-host consumption path of the DOM knowledge data: builds plain
/// <see cref="HashSet{T}"/> lookups from the linked <c>DomKnowledgeData</c> source exactly as
/// the Roslyn template compiler will. The runtime test suite asserts full membership parity
/// between this surface and the net10.0 <c>DomKnowledge</c> frozen tables, pinning the
/// one-authoritative-definition contract of [V01.01.01.03].
/// </summary>
public static class GeneratorHostDomKnowledge
{
    private static readonly HashSet<string> HtmlTagSet = Build(DomKnowledgeData.HtmlTags, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> SvgTagSet = Build(DomKnowledgeData.SvgTags, StringComparer.Ordinal);
    private static readonly HashSet<string> MathTagSet = Build(DomKnowledgeData.MathTags, StringComparer.Ordinal);
    private static readonly HashSet<string> VoidTagSet = Build(DomKnowledgeData.VoidTags, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BooleanAttributeSet = Build(DomKnowledgeData.BooleanAttributes, StringComparer.Ordinal);

    /// <summary>All HTML tags from the shared data definition.</summary>
    public static IEnumerable<string> HtmlTags => HtmlTagSet;

    /// <summary>All SVG tags from the shared data definition.</summary>
    public static IEnumerable<string> SvgTags => SvgTagSet;

    /// <summary>All MathML tags from the shared data definition.</summary>
    public static IEnumerable<string> MathTags => MathTagSet;

    /// <summary>All void tags from the shared data definition.</summary>
    public static IEnumerable<string> VoidTags => VoidTagSet;

    /// <summary>All boolean attributes from the shared data definition.</summary>
    public static IEnumerable<string> BooleanAttributes => BooleanAttributeSet;

    /// <summary>Whether <paramref name="tag"/> is a known HTML element in the generator-host tables.</summary>
    /// <param name="tag">The tag name.</param>
    public static bool IsHtmlTag(string tag) => HtmlTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a known SVG element in the generator-host tables.</summary>
    /// <param name="tag">The tag name.</param>
    public static bool IsSvgTag(string tag) => SvgTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a void element in the generator-host tables.</summary>
    /// <param name="tag">The tag name.</param>
    public static bool IsVoidTag(string tag) => VoidTagSet.Contains(tag);

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
