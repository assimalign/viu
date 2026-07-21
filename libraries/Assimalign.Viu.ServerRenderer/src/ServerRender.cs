using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// The by-name SSR helper library — the C# port of the <c>ssr*</c> runtime helpers the compiled SSR
/// render bodies call into (<c>@vue/server-renderer</c>'s <c>helpers/*</c> plus <c>@vue/shared</c>'s
/// <c>escapeHtml</c>). The pure-string members (<see cref="EscapeHtml(string)"/>,
/// <see cref="SsrRenderAttrs(VirtualNodeProperties?, string?)"/>, <see cref="SsrRenderClass"/>,
/// <see cref="SsrRenderStyle"/>, <see cref="SsrInterpolate"/>, …) are the exact string-producing
/// helpers upstream exposes and are used both by the vnode-walking runtime renderer
/// (<see cref="ServerRenderer"/>) and, later, by the compiler-generated <c>ssrRender</c> bodies
/// ([V01.01.07.02]). The <c>*Async</c> members are the push-based helpers the generated code awaits,
/// forwarding to the same engine the runtime renderer uses so both paths produce byte-identical output.
/// <para>
/// Escaping is security-adjacent and pinned to the exact upstream tables
/// (<c>packages/shared/src/escapeHtml.ts</c>): <see cref="EscapeHtml(string)"/> escapes <c>"</c>, <c>&amp;</c>,
/// <c>'</c>, <c>&lt;</c>, and <c>&gt;</c> — a superset of the WHATWG minimal set, matching Vue's output for
/// both text and attribute values. See https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public static partial class ServerRender
{
    // The five characters escapeHtml rewrites (upstream escapeRE = /["'&<>]/), vectorized so the
    // common no-escape-needed case is an allocation-free scan (SearchValues is the .NET 8+ fast path).
    private static readonly SearchValues<char> EscapableCharacters = SearchValues.Create("\"&'<>");

    /// <summary>
    /// Escapes text or an attribute value for HTML (upstream: <c>escapeHtml</c> in
    /// <c>@vue/shared/src/escapeHtml.ts</c>): <c>"</c> → <c>&amp;quot;</c>, <c>&amp;</c> → <c>&amp;amp;</c>,
    /// <c>'</c> → <c>&amp;#39;</c>, <c>&lt;</c> → <c>&amp;lt;</c>, <c>&gt;</c> → <c>&amp;gt;</c>. Returns the input
    /// unchanged when it contains none of those characters.
    /// </summary>
    /// <param name="value">The raw text; null yields the empty string.</param>
    /// <returns>The escaped text.</returns>
    public static string EscapeHtml(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        var firstIndex = value.AsSpan().IndexOfAny(EscapableCharacters);
        if (firstIndex < 0)
        {
            return value;
        }
        var builder = new StringBuilder(value.Length + 16);
        builder.Append(value, 0, firstIndex);
        for (var index = firstIndex; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '"':
                    builder.Append("&quot;");
                    break;
                case '&':
                    builder.Append("&amp;");
                    break;
                case '\'':
                    builder.Append("&#39;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                default:
                    builder.Append(value[index]);
                    break;
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Escapes an arbitrary value as HTML text, coercing it to its display string first (upstream:
    /// <c>escapeHtml('' + value)</c>). Null yields the empty string.
    /// </summary>
    /// <param name="value">The value to coerce and escape.</param>
    public static string EscapeHtml(object? value) => EscapeHtml(DisplayStringFormatter.ToDisplayString(value));

    /// <summary>
    /// Strips the comment-terminating sequences from comment content so it cannot break out of its
    /// <c>&lt;!-- --&gt;</c> wrapper (upstream: <c>escapeHtmlComment</c>, applied repeatedly until stable so
    /// overlapping sequences cannot reconstitute a terminator).
    /// </summary>
    /// <param name="source">The comment content.</param>
    /// <returns>The sanitized comment content.</returns>
    public static string EscapeHtmlComment(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }
        string previous;
        var current = source;
        do
        {
            previous = current;
            current = CommentStripPattern().Replace(current, string.Empty);
        }
        while (!string.Equals(current, previous, StringComparison.Ordinal));
        return current;
    }

    /// <summary>Renders a comment node (upstream: <c>&lt;!--...--&gt;</c> in <c>renderVNode</c>).</summary>
    /// <param name="content">The comment content; empty yields the <c>&lt;!----&gt;</c> anchor.</param>
    public static string SsrRenderComment(string? content) => "<!--" + EscapeHtmlComment(content) + "-->";

    /// <summary>
    /// Renders an interpolation (upstream: <c>ssrInterpolate</c> = <c>escapeHtml(toDisplayString(value))</c>).
    /// </summary>
    /// <param name="value">The interpolated value.</param>
    public static string SsrInterpolate(object? value) => EscapeHtml(DisplayStringFormatter.ToDisplayString(value));

    /// <summary>
    /// Normalizes and escapes a class binding (upstream: <c>ssrRenderClass</c> =
    /// <c>escapeHtml(normalizeClass(raw))</c>).
    /// </summary>
    /// <param name="value">The class binding: string, list, or name/flag map.</param>
    public static string SsrRenderClass(object? value) => EscapeHtml(StyleAndClassNormalization.NormalizeClass(value));

    /// <summary>
    /// Normalizes, stringifies, and escapes a style binding (upstream: <c>ssrRenderStyle</c>). A string
    /// passes through escaped; a map is normalized then serialized to inline CSS then escaped.
    /// </summary>
    /// <param name="value">The style binding: string, list, or property map.</param>
    public static string SsrRenderStyle(object? value)
    {
        if (value is null || (value is string empty && empty.Length == 0))
        {
            return string.Empty;
        }
        if (value is string text)
        {
            return EscapeHtml(text);
        }
        var normalized = StyleAndClassNormalization.NormalizeStyle(value);
        return EscapeHtml(StyleAndClassNormalization.StringifyStyle(normalized));
    }

    /// <summary>
    /// Serializes a vnode's props to an attribute string (upstream: <c>ssrRenderAttrs</c>). Skips the
    /// reserved props (<c>key</c>, <c>ref</c>, <c>ref_for</c>, <c>ref_key</c>, <c>innerHTML</c>,
    /// <c>textContent</c>), event handlers (<c>onX</c>), <c>.</c>-prefixed force-property bindings, and
    /// <c>value</c> on a <c>&lt;textarea&gt;</c>; strips a leading <c>^</c> force-attribute marker; routes
    /// <c>class</c>/<c>style</c> through their normalizers and <c>className</c> to a direct string; and
    /// serializes the rest through <see cref="SsrRenderDynamicAttr"/>.
    /// </summary>
    /// <param name="properties">The vnode's props, or null.</param>
    /// <param name="tag">The owning element tag (drives the <c>textarea</c> and custom-element rules), or null.</param>
    /// <returns>The attribute string, each attribute preceded by a space; empty when there are none.</returns>
    public static string SsrRenderAttrs(VirtualNodeProperties? properties, string? tag = null)
    {
        if (properties is null || properties.Count == 0)
        {
            return string.Empty;
        }
        var builder = new StringBuilder();
        foreach (var (rawName, value) in properties)
        {
            if (ShouldIgnoreProperty(rawName)
                || IsEventHandlerName(rawName)
                || (string.Equals(tag, "textarea", StringComparison.Ordinal) && string.Equals(rawName, "value", StringComparison.Ordinal))
                || rawName.StartsWith('.'))
            {
                continue;
            }
            // A leading '^' forces attribute rendering (upstream: key.slice(1)).
            var name = rawName.StartsWith('^') ? rawName[1..] : rawName;
            if (string.Equals(name, "class", StringComparison.Ordinal))
            {
                builder.Append(" class=\"").Append(SsrRenderClass(value)).Append('"');
            }
            else if (string.Equals(name, "style", StringComparison.Ordinal))
            {
                builder.Append(" style=\"").Append(SsrRenderStyle(value)).Append('"');
            }
            else if (string.Equals(name, "className", StringComparison.Ordinal))
            {
                // className coerces directly to a string, never through class normalization (upstream).
                if (value is not null)
                {
                    builder.Append(" class=\"").Append(EscapeHtml(DisplayStringFormatter.FormatScalar(value))).Append('"');
                }
            }
            else
            {
                builder.Append(SsrRenderDynamicAttr(name, value, tag));
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Serializes one attribute with a statically known, pre-validated key (upstream: <c>ssrRenderAttr</c>).
    /// Returns the empty string when the value is not renderable.
    /// </summary>
    /// <param name="key">The attribute name (already the correct casing).</param>
    /// <param name="value">The attribute value.</param>
    public static string SsrRenderAttr(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!IsRenderableAttributeValue(value))
        {
            return string.Empty;
        }
        return " " + key + "=\"" + EscapeHtml(DisplayStringFormatter.FormatScalar(value!)) + "\"";
    }

    /// <summary>
    /// Serializes one attribute with a dynamic (unknown) key (upstream: <c>ssrRenderDynamicAttr</c>):
    /// preserves the raw name on custom elements and SVG, else maps to the attribute name or lowercases;
    /// renders boolean attributes by presence; renders a safe name as <c>name="value"</c> (or bare for an
    /// empty string); and skips an SSR-unsafe attribute name.
    /// </summary>
    /// <param name="key">The prop name.</param>
    /// <param name="value">The prop value.</param>
    /// <param name="tag">The owning element tag, or null.</param>
    public static string SsrRenderDynamicAttr(string key, object? value, string? tag = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!IsRenderableAttributeValue(value))
        {
            return string.Empty;
        }
        string attributeKey;
        if (tag is not null && (tag.IndexOf('-', StringComparison.Ordinal) > 0 || DomKnowledge.IsSvgTag(tag)))
        {
            // Custom elements and SVG preserve the author's casing (upstream parity).
            attributeKey = key;
        }
        else
        {
            var mapped = DomKnowledge.GetAttributeName(key);
            attributeKey = string.Equals(mapped, key, StringComparison.Ordinal)
                ? key.ToLowerInvariant()
                : mapped;
        }
        if (DomKnowledge.IsBooleanAttribute(attributeKey))
        {
            return IncludeBooleanAttribute(value) ? " " + attributeKey : string.Empty;
        }
        if (DomKnowledge.IsSsrSafeAttributeName(attributeKey))
        {
            return value is string { Length: 0 }
                ? " " + attributeKey
                : " " + attributeKey + "=\"" + EscapeHtml(DisplayStringFormatter.FormatScalar(value!)) + "\"";
        }
        // Unsafe attribute name (contains '>', '/', '=', quotes, or whitespace/control): skipped rather
        // than escaped, matching upstream's isSSRSafeAttrName gate — the injection-hardening behavior.
        return string.Empty;
    }

    // ==== Push-based async helpers (the surface the compiled ssrRender bodies await) =============

    /// <summary>
    /// Renders a child component (upstream: <c>ssrRenderComponent</c>). Builds the component vnode and runs
    /// its full server lifecycle — setup, <c>ServerPrefetch</c>, subtree — appending to <paramref name="state"/>.
    /// The child inherits <paramref name="parent"/>'s application context and provides.
    /// </summary>
    /// <param name="state">The write surface.</param>
    /// <param name="definition">The component definition.</param>
    /// <param name="properties">The props passed to the child, or null.</param>
    /// <param name="slots">The slot content, or null.</param>
    /// <param name="parent">The rendering (parent) instance.</param>
    public static Task SsrRenderComponentAsync(
        SsrRenderState state,
        IComponentDefinition definition,
        VirtualNodeProperties? properties = null,
        ComponentSlots? slots = null,
        ComponentInstance? parent = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        var componentVirtualNode = VirtualNodeFactory.Component(definition, properties, slots);
        return VirtualNodeSerializer.RenderComponentAsync(state, componentVirtualNode, parent);
    }

    /// <summary>
    /// Renders a slot outlet, wrapped in the fragment hydration anchors (upstream: <c>ssrRenderSlot</c>,
    /// which brackets the content with <c>&lt;!--[--&gt;</c>/<c>&lt;!--]--&gt;</c>). Invokes the named slot with
    /// <paramref name="slotProperties"/> (the scoped-slot scope), falling back to <paramref name="fallback"/>
    /// when the slot is absent or renders empty.
    /// </summary>
    /// <param name="state">The write surface.</param>
    /// <param name="slots">The rendering instance's slots, or null.</param>
    /// <param name="name">The slot name (<c>"default"</c> for the default slot).</param>
    /// <param name="slotProperties">The scoped-slot props, or null.</param>
    /// <param name="parent">The rendering instance (the slot content's descendants' parent).</param>
    /// <param name="fallback">The fallback content factory, or null.</param>
    public static async Task SsrRenderSlotAsync(
        SsrRenderState state,
        ComponentSlots? slots,
        string name,
        object? slotProperties = null,
        ComponentInstance? parent = null,
        Func<VirtualNode?[]?>? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrEmpty(name);
        state.Push(SsrMarkers.FragmentStart);
        // RenderSlot invokes the slot delegate (or fallback) and returns a fragment; walk its children
        // directly so the outer fragment anchors here are the only pair.
        var fragment = VirtualNodeFactory.RenderSlot(slots, name, slotProperties, fallback);
        await VirtualNodeSerializer.RenderChildrenAsync(state, fragment.ArrayChildren, parent).ConfigureAwait(false);
        state.Push(SsrMarkers.FragmentEnd);
    }

    /// <summary>
    /// Iterates a <c>v-for</c> source, awaiting <paramref name="renderItem"/> per entry (upstream:
    /// <c>ssrRenderList</c>). Supports an integer count (<c>n in 5</c>, value one-based), a dictionary
    /// (value, key), and any other enumerable (item, zero-based index). The callback does the pushing, so
    /// this helper writes nothing itself.
    /// </summary>
    /// <param name="source">The iterated source: an <see cref="int"/> count, dictionary, or enumerable.</param>
    /// <param name="renderItem">Renders one entry as <c>(value, key)</c>.</param>
    public static async Task SsrRenderListAsync(object? source, Func<object?, object?, Task> renderItem)
    {
        ArgumentNullException.ThrowIfNull(renderItem);
        switch (source)
        {
            case null:
                break;
            case int count:
                for (var index = 0; index < count; index++)
                {
                    await renderItem(index + 1, index).ConfigureAwait(false);
                }
                break;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    await renderItem(entry.Value, entry.Key).ConfigureAwait(false);
                }
                break;
            case IEnumerable enumerable:
                var enumerableIndex = 0;
                foreach (var item in enumerable)
                {
                    await renderItem(item, enumerableIndex++).ConfigureAwait(false);
                }
                break;
        }
    }

    /// <summary>
    /// Renders a <c>&lt;Teleport&gt;</c> (upstream: <c>ssrRenderTeleport</c>): the origin position gets the
    /// <c>&lt;!--teleport start--&gt;</c>/<c>&lt;!--teleport end--&gt;</c> anchor pair, while
    /// <paramref name="contentRenderer"/>'s output is buffered against <paramref name="target"/> in the
    /// <see cref="SsrContext"/> (with the trailing <c>&lt;!--teleport anchor--&gt;</c>). A disabled teleport
    /// renders its content in place instead, leaving only the anchor in the target buffer.
    /// </summary>
    /// <param name="state">The write surface.</param>
    /// <param name="contentRenderer">Renders the teleport content into the supplied state.</param>
    /// <param name="target">The target selector (the <c>to</c> prop); a null/empty target skips the content.</param>
    /// <param name="disabled">Whether the teleport is disabled (content stays in place).</param>
    public static async Task SsrRenderTeleportAsync(
        SsrRenderState state,
        Func<SsrRenderState, Task> contentRenderer,
        string? target,
        bool disabled)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contentRenderer);
        state.Push(SsrMarkers.TeleportStart);
        if (string.IsNullOrEmpty(target))
        {
            state.Push(SsrMarkers.TeleportEnd);
            return;
        }
        if (disabled)
        {
            await contentRenderer(state).ConfigureAwait(false);
            state.Context.AppendTeleport(target, SsrMarkers.TeleportAnchor);
        }
        else
        {
            var bufferWriter = new SsrWriter();
            var bufferState = new SsrRenderState(bufferWriter, state.Context, state.CancellationToken);
            await contentRenderer(bufferState).ConfigureAwait(false);
            bufferState.Push(SsrMarkers.TeleportAnchor);
            state.Context.AppendTeleport(target, bufferWriter.ToStringResult());
        }
        state.Push(SsrMarkers.TeleportEnd);
    }

    /// <summary>
    /// Renders a <c>&lt;Suspense&gt;</c>'s default branch, awaiting its async dependencies server-side
    /// (upstream: <c>ssrRenderSuspense</c> renders and awaits the <c>default</c> slot; SSR never shows the
    /// fallback). Because the walk already awaits each descendant component's <c>ServerPrefetch</c>, invoking
    /// <paramref name="defaultBranch"/> resolves the branch's async data before returning. Full boundary
    /// semantics (the fallback branch, error capture) arrive with the runtime Suspense component
    /// ([V01.01.03.20]).
    /// </summary>
    /// <param name="state">The write surface.</param>
    /// <param name="defaultBranch">Renders the default-branch content into the supplied state.</param>
    public static Task SsrRenderSuspenseAsync(SsrRenderState state, Func<SsrRenderState, Task> defaultBranch)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(defaultBranch);
        return defaultBranch(state);
    }

    // Upstream shouldIgnoreProp = makeMap(`,key,ref,innerHTML,textContent,ref_key,ref_for`); the leading
    // comma admits the empty-string key. innerHTML/textContent are applied as child overrides, not attrs.
    private static bool ShouldIgnoreProperty(string name) => name switch
    {
        "" or "key" or "ref" or "innerHTML" or "textContent" or "ref_key" or "ref_for" => true,
        _ => false,
    };

    // Upstream isOn = /^on[^a-z]/: 'on' followed by a non-lowercase-letter (onClick, on:foo, on-x), so a
    // word like "onions" is not treated as a handler.
    private static bool IsEventHandlerName(string name)
        => name.Length > 2 && name[0] == 'o' && name[1] == 'n' && !char.IsAsciiLetterLower(name[2]);

    // Upstream isRenderableAttrValue: string | number | boolean (and not null). Objects/arrays are skipped.
    private static bool IsRenderableAttributeValue(object? value) => value switch
    {
        null => false,
        string => true,
        bool => true,
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => true,
        _ => false,
    };

    // Upstream includeBooleanAttr = !!value || value === '': any truthy value or the empty string.
    private static bool IncludeBooleanAttribute(object? value)
        => StyleAndClassNormalization.IsTruthy(value) || value is string { Length: 0 };

    // Upstream commentStripRE = /^(?:-?>)+|<!--|-->|--!>|<!-$/g (JS [^] -> .NET . with no special flags
    // needed here). Applied repeatedly by EscapeHtmlComment so overlapping matches cannot re-form a
    // terminator.
    [GeneratedRegex("^(?:-?>)+|<!--|-->|--!>|<!-$")]
    private static partial Regex CommentStripPattern();
}
