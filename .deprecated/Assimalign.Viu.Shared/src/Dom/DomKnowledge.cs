using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Assimalign.Viu.Shared;

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

    // Span alternate lookups (net9+): the template compiler tokenizes source text into spans,
    // so membership tests must not materialize a string per tag/attribute token. Declared
    // after the sets they view (field initializers run in declaration order).
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> HtmlTagLookup =
        HtmlTagSet.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> SvgTagLookup =
        SvgTagSet.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> MathTagLookup =
        MathTagSet.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> VoidTagLookup =
        VoidTagSet.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> BooleanAttributeLookup =
        BooleanAttributeSet.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> KnownHtmlAttributeLookup =
        KnownHtmlAttributeSet.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> KnownSvgAttributeLookup =
        KnownSvgAttributeSet.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> PropertyToAttributeLookup =
        PropertyToAttributeMap.GetAlternateLookup<ReadOnlySpan<char>>();

    // The unsafe characters plus the full C0 control range, vectorized (SearchValues is the
    // .NET 8+ fast path for repeated membership scans over a fixed character set).
    private static readonly SearchValues<char> UnsafeAttributeNameSearchValues = BuildUnsafeAttributeNameSearchValues();

    /// <summary>Whether <paramref name="tag"/> is a known HTML element (upstream: <c>isHTMLTag</c>).</summary>
    /// <param name="tag">The tag name (case-insensitive per WHATWG).</param>
    public static bool IsHtmlTag(string tag) => HtmlTagSet.Contains(tag);

    /// <inheritdoc cref="IsHtmlTag(string)"/>
    /// <remarks>Allocation-free for span-shaped tokens (compiler tokenization).</remarks>
    public static bool IsHtmlTag(ReadOnlySpan<char> tag) => HtmlTagLookup.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a known SVG element (upstream: <c>isSVGTag</c>).</summary>
    /// <param name="tag">The tag name (case-sensitive per SVG 2).</param>
    public static bool IsSvgTag(string tag) => SvgTagSet.Contains(tag);

    /// <inheritdoc cref="IsSvgTag(string)"/>
    /// <remarks>Allocation-free for span-shaped tokens (compiler tokenization).</remarks>
    public static bool IsSvgTag(ReadOnlySpan<char> tag) => SvgTagLookup.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a known MathML element (upstream: <c>isMathMLTag</c>).</summary>
    /// <param name="tag">The tag name (case-sensitive per MathML Core).</param>
    public static bool IsMathMLTag(string tag) => MathTagSet.Contains(tag);

    /// <inheritdoc cref="IsMathMLTag(string)"/>
    /// <remarks>Allocation-free for span-shaped tokens (compiler tokenization).</remarks>
    public static bool IsMathMLTag(ReadOnlySpan<char> tag) => MathTagLookup.Contains(tag);

    /// <summary>Whether <paramref name="tag"/> is a void element (upstream: <c>isVoidTag</c>, WHATWG).</summary>
    /// <param name="tag">The tag name.</param>
    public static bool IsVoidTag(string tag) => VoidTagSet.Contains(tag);

    /// <inheritdoc cref="IsVoidTag(string)"/>
    /// <remarks>Allocation-free for span-shaped tokens (compiler tokenization).</remarks>
    public static bool IsVoidTag(ReadOnlySpan<char> tag) => VoidTagLookup.Contains(tag);

    /// <summary>Whether <paramref name="attributeName"/> is a boolean attribute (upstream: <c>isBooleanAttr</c>).</summary>
    /// <param name="attributeName">The attribute name.</param>
    public static bool IsBooleanAttribute(string attributeName) => BooleanAttributeSet.Contains(attributeName);

    /// <inheritdoc cref="IsBooleanAttribute(string)"/>
    /// <remarks>Allocation-free for span-shaped tokens (compiler tokenization).</remarks>
    public static bool IsBooleanAttribute(ReadOnlySpan<char> attributeName) => BooleanAttributeLookup.Contains(attributeName);

    /// <summary>Whether <paramref name="attributeName"/> is a known HTML attribute (upstream: <c>isKnownHtmlAttr</c>).</summary>
    /// <param name="attributeName">The attribute name.</param>
    public static bool IsKnownHtmlAttribute(string attributeName) => KnownHtmlAttributeSet.Contains(attributeName);

    /// <inheritdoc cref="IsKnownHtmlAttribute(string)"/>
    /// <remarks>Allocation-free for span-shaped tokens (compiler tokenization).</remarks>
    public static bool IsKnownHtmlAttribute(ReadOnlySpan<char> attributeName) => KnownHtmlAttributeLookup.Contains(attributeName);

    /// <summary>Whether <paramref name="attributeName"/> is a known SVG attribute (upstream: <c>isKnownSvgAttr</c>).</summary>
    /// <param name="attributeName">The attribute name.</param>
    public static bool IsKnownSvgAttribute(string attributeName) => KnownSvgAttributeSet.Contains(attributeName);

    /// <inheritdoc cref="IsKnownSvgAttribute(string)"/>
    /// <remarks>Allocation-free for span-shaped tokens (compiler tokenization).</remarks>
    public static bool IsKnownSvgAttribute(ReadOnlySpan<char> attributeName) => KnownSvgAttributeLookup.Contains(attributeName);

    /// <summary>
    /// Whether <paramref name="attributeName"/> is safe to serialize in SSR output (upstream:
    /// <c>isSSRSafeAttrName</c>): names containing <c>&gt;</c>, <c>/</c>, <c>=</c>, quotes,
    /// or whitespace/control characters are excluded rather than escaped.
    /// </summary>
    /// <param name="attributeName">The attribute name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="attributeName"/> is null.</exception>
    public static bool IsSsrSafeAttributeName(string attributeName)
    {
        ArgumentNullException.ThrowIfNull(attributeName);
        return IsSsrSafeAttributeName(attributeName.AsSpan());
    }

    /// <inheritdoc cref="IsSsrSafeAttributeName(string)"/>
    /// <remarks>Vectorized over <see cref="SearchValues{T}"/>.</remarks>
    public static bool IsSsrSafeAttributeName(ReadOnlySpan<char> attributeName)
        => attributeName.Length > 0 && !attributeName.ContainsAny(UnsafeAttributeNameSearchValues);

    /// <summary>
    /// Maps a camelCase prop name to its attribute name, covering upstream's
    /// <c>propsToAttrMap</c> casing exceptions (<c>className</c> → <c>class</c>,
    /// <c>htmlFor</c> → <c>for</c>, <c>acceptCharset</c> → <c>accept-charset</c>,
    /// <c>httpEquiv</c> → <c>http-equiv</c>); other names pass through unchanged.
    /// </summary>
    /// <param name="propertyName">The prop name.</param>
    public static string GetAttributeName(string propertyName)
        => PropertyToAttributeMap.TryGetValue(propertyName, out var attributeName) ? attributeName : propertyName;

    /// <summary>
    /// Looks up a casing-exception attribute name for a span-shaped prop token, allocation-free
    /// (compiler tokenization): returns false for pass-through names so the caller keeps its
    /// span instead of materializing a string.
    /// </summary>
    /// <param name="propertyName">The prop name token.</param>
    /// <param name="attributeName">The mapped attribute name when an exception applies.</param>
    public static bool TryGetAttributeName(ReadOnlySpan<char> propertyName, out string attributeName)
        => PropertyToAttributeLookup.TryGetValue(propertyName, out attributeName!);

    private static SearchValues<char> BuildUnsafeAttributeNameSearchValues()
    {
        // The named unsafe characters plus the full C0 control range in one vectorized set
        // (tab/newline/form feed appear in both; SearchValues deduplicates).
        Span<char> characters = stackalloc char[DomKnowledgeData.UnsafeAttributeNameCharacters.Length + 32];
        DomKnowledgeData.UnsafeAttributeNameCharacters.CopyTo(characters);
        for (var control = 0; control < 32; control++)
        {
            characters[DomKnowledgeData.UnsafeAttributeNameCharacters.Length + control] = (char)control;
        }
        return SearchValues.Create(characters);
    }

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
