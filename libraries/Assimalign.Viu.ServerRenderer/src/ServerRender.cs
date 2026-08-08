using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Provides the public entry points and by-name helpers used by runtime and compiler-produced
/// server rendering.
/// </summary>
/// <remarks>
/// Compiled rendering is an optimization over the same escaping, attribute, marker, teleport, and
/// lease-backed traversal semantics as <see cref="ServerRenderer"/>. HTML escaping targets the
/// WHATWG fragment-serialization format and is reflection-free for trimming and AOT safety.
/// Specified by <c>[SSR-1]</c>, <c>[SSR-3]</c>, and <c>[SSR-6]</c>.
/// </remarks>
public static partial class ServerRender
{
    private static readonly SearchValues<char> EscapableCharacters =
        SearchValues.Create("\"&'<>");
    private static readonly IReadOnlyDictionary<string, object?> EmptySlotArguments =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    /// <summary>Renders one configured application to a completed HTML string.</summary>
    /// <param name="application">The immutable per-render application composition.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for component prefetch and rendering.</param>
    /// <returns>The completed WHATWG HTML serialization.</returns>
    /// <remarks>Specified by <c>[SSR-1]</c> and <c>[SSR-2]</c>.</remarks>
    public static Task<string> RenderToStringAsync(
        ServerRenderApplication application,
        SsrContext? context = null,
        CancellationToken cancellationToken = default) =>
        ServerRenderer.RenderToStringAsync(application, context, cancellationToken);

    /// <summary>Renders one host-neutral virtual tree to a completed HTML string.</summary>
    /// <param name="rootComponent">The root virtual node.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for rendering.</param>
    /// <returns>The completed WHATWG HTML serialization.</returns>
    /// <remarks>Specified by <c>[SSR-1]</c> and <c>[SSR-3]</c>.</remarks>
    public static Task<string> RenderToStringAsync(
        VirtualNode rootComponent,
        SsrContext? context = null,
        CancellationToken cancellationToken = default) =>
        ServerRenderer.RenderToStringAsync(rootComponent, context, cancellationToken);

    /// <summary>Streams one configured application with component-subtree flush boundaries.</summary>
    /// <param name="application">The immutable per-render application composition.</param>
    /// <param name="writer">The externally owned destination writer.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for prefetch, writes, and flushes.</param>
    /// <returns>A task completing after all produced content has flushed.</returns>
    /// <remarks>Specified by <c>[SSR-1]</c>, <c>[SSR-4]</c>, and <c>[SSR-10]</c>.</remarks>
    public static Task RenderToStreamAsync(
        ServerRenderApplication application,
        TextWriter writer,
        SsrContext? context = null,
        CancellationToken cancellationToken = default) =>
        ServerRenderer.RenderToStreamAsync(application, writer, context, cancellationToken);

    /// <summary>Streams one host-neutral virtual tree to a text writer.</summary>
    /// <param name="rootComponent">The root virtual node.</param>
    /// <param name="writer">The externally owned destination writer.</param>
    /// <param name="context">The per-render state handoff context, or null to create one.</param>
    /// <param name="cancellationToken">Cancellation for writes and flushes.</param>
    /// <returns>A task completing after all produced content has flushed.</returns>
    /// <remarks>Specified by <c>[SSR-1]</c> and <c>[SSR-3]</c>.</remarks>
    public static Task RenderToStreamAsync(
        VirtualNode rootComponent,
        TextWriter writer,
        SsrContext? context = null,
        CancellationToken cancellationToken = default) =>
        ServerRenderer.RenderToStreamAsync(rootComponent, writer, context, cancellationToken);

    /// <summary>
    /// Escapes quotation marks, ampersands, apostrophes, less-than signs, and greater-than signs
    /// for HTML text or attribute content.
    /// </summary>
    /// <param name="value">The raw text; null yields an empty string.</param>
    /// <returns>The escaped value, or the original string when no character needs escaping.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string EscapeHtml(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        int firstIndex = value.AsSpan().IndexOfAny(EscapableCharacters);
        if (firstIndex < 0)
        {
            return value;
        }

        StringBuilder builder = new(value.Length + 16);
        builder.Append(value, 0, firstIndex);
        for (int index = firstIndex; index < value.Length; index++)
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

    /// <summary>Formats an arbitrary value and escapes it as HTML text.</summary>
    /// <param name="value">The interpolation or attribute value; null yields an empty string.</param>
    /// <returns>The deterministic escaped display string.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string EscapeHtml(object? value) =>
        EscapeHtml(DisplayStringFormatter.ToDisplayString(value));

    /// <summary>Repeatedly removes sequences that can terminate or reopen an HTML comment.</summary>
    /// <param name="source">The raw comment content; null yields an empty string.</param>
    /// <returns>Content safe to place between HTML comment delimiters.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string EscapeHtmlComment(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        string previous;
        string current = source;
        do
        {
            previous = current;
            current = CommentStripPattern().Replace(current, string.Empty);
        }
        while (!string.Equals(current, previous, StringComparison.Ordinal));

        return current;
    }

    /// <summary>Serializes one comment node, including the stable empty-comment anchor.</summary>
    /// <param name="content">The raw comment content.</param>
    /// <returns>The complete serialized comment.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c> and <c>[SSR-MARKERS-1]</c>.</remarks>
    public static string SsrRenderComment(string? content) =>
        string.Concat("<!--", EscapeHtmlComment(content), "-->");

    /// <summary>Formats and escapes one template interpolation.</summary>
    /// <param name="value">The interpolation value.</param>
    /// <returns>The escaped display string.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string SsrInterpolate(object? value) =>
        EscapeHtml(DisplayStringFormatter.ToDisplayString(value));

    /// <summary>Normalizes, flattens, and escapes one class binding.</summary>
    /// <param name="value">A supported class binding shape.</param>
    /// <returns>The escaped ordered class string.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string SsrRenderClass(object? value) =>
        EscapeHtml(StyleAndClassNormalization.NormalizeClass(value));

    /// <summary>Normalizes, stringifies, and escapes one style binding.</summary>
    /// <param name="value">A supported style binding shape.</param>
    /// <returns>The escaped inline CSS string.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string SsrRenderStyle(object? value)
    {
        if (value is null || value is string { Length: 0 })
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return EscapeHtml(text);
        }

        object? normalized = StyleAndClassNormalization.NormalizeStyle(value);
        return EscapeHtml(StyleAndClassNormalization.StringifyStyle(normalized));
    }

    /// <summary>Serializes the attribute bindings for one element.</summary>
    /// <param name="bindings">The immutable host bindings, or null.</param>
    /// <param name="elementName">The optional owning element name used for casing and textarea rules.</param>
    /// <returns>Serialized attributes, each including its leading space.</returns>
    /// <remarks>
    /// Property and event bindings are excluded. Class and style are normalized, boolean attributes
    /// render by presence, and unsafe dynamic names are dropped. Specified by <c>[SSR-6]</c>.
    /// </remarks>
    public static string SsrRenderAttributes(
        IReadOnlyList<ElementBinding>? bindings,
        QualifiedName? elementName = null)
    {
        if (bindings is null || bindings.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        for (int index = 0; index < bindings.Count; index++)
        {
            ElementBinding binding = bindings[index];
            if (binding.Kind != ElementBindingKind.Attribute)
            {
                continue;
            }

            string rawName = binding.Name.ToString();
            if (ShouldIgnoreAttribute(rawName)
                || IsEventHandlerName(rawName)
                || rawName.StartsWith(".", StringComparison.Ordinal)
                || (elementName is { } owner
                    && string.Equals(
                        owner.LocalName,
                        "textarea",
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        binding.Name.LocalName,
                        "value",
                        StringComparison.Ordinal)))
            {
                continue;
            }

            string name = rawName.StartsWith('^') ? rawName[1..] : rawName;
            object? value = binding.Value;
            if (string.Equals(name, "class", StringComparison.Ordinal))
            {
                builder.Append(" class=\"");
                builder.Append(SsrRenderClass(value));
                builder.Append('"');
            }
            else if (string.Equals(name, "style", StringComparison.Ordinal))
            {
                builder.Append(" style=\"");
                builder.Append(SsrRenderStyle(value));
                builder.Append('"');
            }
            else if (string.Equals(name, "className", StringComparison.Ordinal))
            {
                if (IsRenderableAttributeValue(value))
                {
                    builder.Append(" class=\"");
                    builder.Append(EscapeHtml(DisplayStringFormatter.FormatScalar(value!)));
                    builder.Append('"');
                }
            }
            else
            {
                bool preserveCase = elementName is { } namedOwner
                    && HtmlSerializationRules.ShouldPreserveAttributeCase(namedOwner);
                builder.Append(RenderDynamicAttribute(name, value, preserveCase));
            }
        }

        return builder.ToString();
    }

    /// <summary>Serializes one statically validated attribute name and scalar value.</summary>
    /// <param name="key">The prevalidated attribute name.</param>
    /// <param name="value">The scalar value.</param>
    /// <returns>The attribute with a leading space, or an empty string for an unsupported value.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string SsrRenderAttribute(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!IsRenderableAttributeValue(value))
        {
            return string.Empty;
        }

        return string.Concat(
            " ",
            key,
            "=\"",
            EscapeHtml(DisplayStringFormatter.FormatScalar(value!)),
            "\"");
    }

    /// <summary>Serializes one dynamically named attribute.</summary>
    /// <param name="key">The untrusted attribute or property name.</param>
    /// <param name="value">The scalar value.</param>
    /// <param name="tag">The optional owning tag used to preserve SVG and custom-element casing.</param>
    /// <returns>The attribute with a leading space, or an empty string when unsafe or absent.</returns>
    /// <remarks>Specified by <c>[SSR-6]</c>.</remarks>
    public static string SsrRenderDynamicAttribute(
        string key,
        object? value,
        string? tag = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return RenderDynamicAttribute(
            key,
            value,
            HtmlSerializationRules.ShouldPreserveAttributeCase(tag));
    }

    /// <summary>Serializes a child tree through the same lease-backed runtime traversal.</summary>
    /// <param name="state">The active renderer-owned write state.</param>
    /// <param name="component">The child tree value.</param>
    /// <param name="parent">The active parent lease for nested component activation.</param>
    /// <returns>A task completing after the child serializes.</returns>
    /// <remarks>
    /// Passing the lease rather than a context prevents capability casts and preserves nested
    /// ancestry under <c>[SSR-10]</c>.
    /// </remarks>
    public static Task SsrRenderComponentAsync(
        SsrRenderState state,
        VirtualNode? component,
        IComponentRenderScope? parent = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return ServerMarkupSerializer.RenderAsync(state, component, parent);
    }

    /// <summary>Renders one named slot inside fragment hydration anchors.</summary>
    /// <param name="state">The active renderer-owned write state.</param>
    /// <param name="slots">The invocation's lazy slot map, or null.</param>
    /// <param name="name">The non-empty slot name.</param>
    /// <param name="slotArguments">The scoped-slot arguments, or null for an empty map.</param>
    /// <param name="parent">The active parent render lease.</param>
    /// <param name="fallback">Fallback content used when the slot is absent or empty.</param>
    /// <returns>A task completing after the anchored slot range serializes.</returns>
    /// <remarks>Specified by <c>[SSR-MARKERS-1]</c> and <c>[SSR-10]</c>.</remarks>
    public static async Task SsrRenderSlotAsync(
        SsrRenderState state,
        IReadOnlyDictionary<string, ComponentSlot>? slots,
        string name,
        IReadOnlyDictionary<string, object?>? slotArguments = null,
        IComponentRenderScope? parent = null,
        Func<VirtualNode?>? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrEmpty(name);
        state.Push(HydrationMarkers.FragmentStart);

        VirtualNode? content = null;
        if (slots is not null && slots.TryGetValue(name, out ComponentSlot? slot))
        {
            content = slot(slotArguments ?? EmptySlotArguments);
        }

        content ??= fallback?.Invoke();
        await ServerMarkupSerializer.RenderAsync(state, content, parent).ConfigureAwait(false);
        state.Push(HydrationMarkers.FragmentEnd);
    }

    /// <summary>Iterates a count, dictionary, or enumerable for compiler-produced list rendering.</summary>
    /// <param name="source">An integer count, dictionary, enumerable, or null.</param>
    /// <param name="renderItem">The asynchronous item renderer receiving value then key/index.</param>
    /// <returns>A task completing after every item renderer has completed in source order.</returns>
    /// <remarks>List rendering is deterministic and allocation-safe under <c>[SSR-3]</c>.</remarks>
    public static async Task SsrRenderListAsync(
        object? source,
        Func<object?, object?, Task> renderItem)
    {
        ArgumentNullException.ThrowIfNull(renderItem);
        switch (source)
        {
            case null:
                break;
            case int count:
                for (int index = 0; index < count; index++)
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
                int enumerableIndex = 0;
                foreach (object? item in enumerable)
                {
                    await renderItem(item, enumerableIndex++).ConfigureAwait(false);
                }

                break;
        }
    }

    /// <summary>Serializes teleport origin markers and target-buffer content.</summary>
    /// <param name="state">The active renderer-owned write state.</param>
    /// <param name="contentRenderer">The callback that serializes teleport children.</param>
    /// <param name="target">The target identifier; null or empty skips target content.</param>
    /// <param name="disabled">Whether content remains at the origin.</param>
    /// <returns>A task completing after origin and target contributions are complete.</returns>
    /// <remarks>Specified by <c>[SSR-7]</c> and <c>[SSR-MARKERS-2]</c>.</remarks>
    public static async Task SsrRenderTeleportAsync(
        SsrRenderState state,
        Func<SsrRenderState, Task> contentRenderer,
        string? target,
        bool disabled)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contentRenderer);
        state.Push(HydrationMarkers.TeleportStart);
        if (string.IsNullOrEmpty(target))
        {
            state.Push(HydrationMarkers.TeleportEnd);
            return;
        }

        if (disabled)
        {
            await contentRenderer(state).ConfigureAwait(false);
            state.Context.AppendTeleport(target, HydrationMarkers.TeleportAnchor);
        }
        else
        {
            SsrWriter bufferWriter = new();
            SsrRenderState bufferState = state.CreateBuffer(bufferWriter);
            await contentRenderer(bufferState).ConfigureAwait(false);
            bufferState.Push(HydrationMarkers.TeleportAnchor);
            state.Context.AppendTeleport(target, bufferWriter.ToStringResult());
        }

        state.Push(HydrationMarkers.TeleportEnd);
    }

    /// <summary>Renders only a suspense boundary's resolved default branch on the server.</summary>
    /// <param name="state">The active renderer-owned write state.</param>
    /// <param name="defaultBranch">The asynchronous default-branch renderer.</param>
    /// <returns>The branch task, including descendant prefetch waits.</returns>
    /// <remarks>The fallback never appears in server output, as specified by <c>[SSR-4]</c>.</remarks>
    public static Task SsrRenderSuspenseAsync(
        SsrRenderState state,
        Func<SsrRenderState, Task> defaultBranch)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(defaultBranch);
        return defaultBranch(state);
    }

    private static string RenderDynamicAttribute(
        string key,
        object? value,
        bool preserveCase)
    {
        if (!IsRenderableAttributeValue(value))
        {
            return string.Empty;
        }

        string mapped = preserveCase
            ? key
            : HtmlSerializationRules.GetAttributeName(key);
        string attributeKey = preserveCase
            || !string.Equals(mapped, key, StringComparison.Ordinal)
            ? mapped
            : key.ToLowerInvariant();

        if (HtmlSerializationRules.IsBooleanAttribute(attributeKey))
        {
            return IncludeBooleanAttribute(value)
                ? string.Concat(" ", attributeKey)
                : string.Empty;
        }

        if (!HtmlSerializationRules.IsSsrSafeAttributeName(attributeKey))
        {
            return string.Empty;
        }

        return value is string { Length: 0 }
            ? string.Concat(" ", attributeKey)
            : string.Concat(
                " ",
                attributeKey,
                "=\"",
                EscapeHtml(DisplayStringFormatter.FormatScalar(value!)),
                "\"");
    }

    private static bool ShouldIgnoreAttribute(string name) => name switch
    {
        "" or "key" or "ref" or "innerHTML" or "textContent" or "ref_key" or "ref_for" => true,
        _ => false,
    };

    private static bool IsEventHandlerName(string name) =>
        name.Length > 2
        && name[0] == 'o'
        && name[1] == 'n'
        && !char.IsAsciiLetterLower(name[2]);

    private static bool IsRenderableAttributeValue(object? value) => value switch
    {
        null => false,
        string => true,
        bool => true,
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double
            or decimal => true,
        _ => false,
    };

    private static bool IncludeBooleanAttribute(object? value) =>
        StyleAndClassNormalization.IsTruthy(value) || value is string { Length: 0 };

    [GeneratedRegex("^(?:-?>)+|<!--|-->|--!>|<!-$")]
    private static partial Regex CommentStripPattern();
}
