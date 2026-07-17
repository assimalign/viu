using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Assimalign.Vue.Shared;

/// <summary>
/// The frozen DOM knowledge tables — the C# port of <c>@vue/shared</c>'s
/// <c>domTagConfig.ts</c>/<c>domAttrConfig.ts</c>. The template compiler resolves native
/// elements vs components and infers namespaces from these at build time; runtime-dom uses
/// them for attribute-vs-property decisions; the SSR renderer uses them for void-tag and
/// boolean-attribute serialization. Lookups are allocation-free over
/// <see cref="FrozenSet{T}"/>/<see cref="FrozenDictionary{TKey,TValue}"/> built from the
/// single authoritative data definition (<c>Internal/DomKnowledgeData.cs</c>, the linked
/// shared-source seam for the netstandard2.0 generator host — see that file's header).
/// HTML tag lookups are case-insensitive per WHATWG; SVG and MathML are case-sensitive per
/// their specs.
/// </summary>
public static class DomKnowledge
{
    private static readonly FrozenSet<string> HtmlTagSet =
        DomKnowledgeData.HtmlTags.Split(',').ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> SvgTagSet =
        DomKnowledgeData.SvgTags.Split(',').ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MathTagSet =
        DomKnowledgeData.MathTags.Split(',').ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> VoidTagSet =
        DomKnowledgeData.VoidTags.Split(',').ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> BooleanAttributeSet =
        DomKnowledgeData.BooleanAttributes.Split(',').ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> KnownHtmlAttributeSet =
        DomKnowledgeData.KnownHtmlAttributes.Split(',').ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> KnownSvgAttributeSet =
        DomKnowledgeData.KnownSvgAttributes.Split(',').ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> PropertyToAttributeMap = BuildPropertyToAttributeMap();

    /// <summary>Whether <paramref name="tag"/> is a known HTML element (upstream: <c>isHTMLTag</c>).</summary>
    /// <param name="tag">The tag name (case-insensitive per WHATWG).</param>
    public static bool IsHtmlTag(string tag) => HtmlTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a known SVG element (upstream: <c>isSVGTag</c>).</summary>
    /// <param name="tag">The tag name (case-sensitive per SVG 2).</param>
    public static bool IsSvgTag(string tag) => SvgTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a known MathML element (upstream: <c>isMathMLTag</c>).</summary>
    /// <param name="tag">The tag name (case-sensitive per MathML Core).</param>
    public static bool IsMathMLTag(string tag) => MathTagSet.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a void element (upstream: <c>isVoidTag</c>, WHATWG).</summary>
    /// <param name="tag">The tag name.</param>
    public static bool IsVoidTag(string tag) => VoidTagSet.Contains(tag);

    /// <summary>Whether <paramref name="attributeName"/> is a boolean attribute (upstream: <c>isBooleanAttr</c>).</summary>
    /// <param name="attributeName">The attribute name.</param>
    public static bool IsBooleanAttribute(string attributeName) => BooleanAttributeSet.Contains(attributeName);

    /// <summary>Whether <paramref name="attributeName"/> is a known HTML attribute (upstream: <c>isKnownHtmlAttr</c>).</summary>
    /// <param name="attributeName">The attribute name.</param>
    public static bool IsKnownHtmlAttribute(string attributeName) => KnownHtmlAttributeSet.Contains(attributeName);

    /// <summary>Whether <paramref name="attributeName"/> is a known SVG attribute (upstream: <c>isKnownSvgAttr</c>).</summary>
    /// <param name="attributeName">The attribute name.</param>
    public static bool IsKnownSvgAttribute(string attributeName) => KnownSvgAttributeSet.Contains(attributeName);

    /// <summary>
    /// Whether <paramref name="attributeName"/> is safe to serialize in SSR output (upstream:
    /// <c>isSSRSafeAttrName</c>): names containing <c>&gt;</c>, <c>/</c>, <c>=</c>, quotes,
    /// or whitespace/control characters are excluded rather than escaped.
    /// </summary>
    /// <param name="attributeName">The attribute name.</param>
    public static bool IsSsrSafeAttributeName(string attributeName)
    {
        ArgumentNullException.ThrowIfNull(attributeName);
        foreach (var character in attributeName)
        {
            if (character < ' ' || DomKnowledgeData.UnsafeAttributeNameCharacters.IndexOf(character) >= 0)
            {
                return false;
            }
        }
        return attributeName.Length > 0;
    }

    /// <summary>
    /// Maps a camelCase prop name to its attribute name, covering upstream's
    /// <c>propsToAttrMap</c> casing exceptions (<c>className</c> → <c>class</c>,
    /// <c>htmlFor</c> → <c>for</c>, <c>acceptCharset</c> → <c>accept-charset</c>,
    /// <c>httpEquiv</c> → <c>http-equiv</c>); other names pass through unchanged.
    /// </summary>
    /// <param name="propertyName">The prop name.</param>
    public static string GetAttributeName(string propertyName)
        => PropertyToAttributeMap.TryGetValue(propertyName, out var attributeName) ? attributeName : propertyName;

    private static FrozenDictionary<string, string> BuildPropertyToAttributeMap()
    {
        var pairs = DomKnowledgeData.PropertyToAttributePairs;
        var map = new Dictionary<string, string>(pairs.Length / 2, StringComparer.Ordinal);
        for (var index = 0; index < pairs.Length; index += 2)
        {
            map[pairs[index]] = pairs[index + 1];
        }
        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
