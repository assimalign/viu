using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// Creates and combines <see cref="VirtualNode"/> instances — the C# port of
/// <c>createVNode</c>/<c>h</c>, <c>cloneVNode</c>, <c>mergeProps</c>, <c>isVNode</c>, and
/// <c>normalizeVNode</c> from <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/vnode.ts</c>, https://vuejs.org/api/render-function.html).
/// Children normalize per Vue semantics at creation: a string becomes text children, an array
/// becomes array children with null entries turned into comment placeholders. The <c>"key"</c>
/// and <c>"ref"</c> props are extracted onto <see cref="VirtualNode.Key"/> and
/// <see cref="VirtualNode.Reference"/> at creation (they remain in the bag but are reserved —
/// the renderer never patches them).
/// </summary>
public static class VirtualNodeFactory
{
    /// <summary>Creates an element vnode with no properties or children.</summary>
    /// <param name="tag">The element tag.</param>
    public static VirtualNode Element(string tag)
        => Element(tag, null, (VirtualNode?[]?)null);

    /// <summary>Creates an element vnode with child vnodes.</summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="children">The children; null entries become comment placeholders.</param>
    public static VirtualNode Element(string tag, params VirtualNode?[]? children)
        => Element(tag, null, children);

    /// <summary>Creates an element vnode with text children.</summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="textChildren">The text content.</param>
    public static VirtualNode Element(string tag, string textChildren)
        => Element(tag, null, textChildren);

    /// <summary>Creates an element vnode with properties and text children.</summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="properties">The properties, or null.</param>
    /// <param name="textChildren">The text content.</param>
    public static VirtualNode Element(string tag, VirtualNodeProperties? properties, string textChildren)
        => Element(tag, properties, textChildren, default, null);

    /// <summary>Creates an element vnode with properties and child vnodes.</summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="properties">The properties, or null.</param>
    /// <param name="children">The children; null entries become comment placeholders.</param>
    public static VirtualNode Element(string tag, VirtualNodeProperties? properties, params VirtualNode?[]? children)
        => Element(tag, properties, children, default, null);

    /// <summary>
    /// Creates a compiler-shaped element vnode carrying patch hints (upstream:
    /// <c>createVNode(type, props, children, patchFlag, dynamicProps)</c>). A positive
    /// <paramref name="patchFlag"/> promises the vnode follows the compiled patch contract.
    /// </summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="properties">The properties, or null.</param>
    /// <param name="textChildren">The text content.</param>
    /// <param name="patchFlag">The compiler patch hint.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> has <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode Element(
        string tag,
        VirtualNodeProperties? properties,
        string textChildren,
        PatchFlags patchFlag,
        string[]? dynamicProperties = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        return new VirtualNode(VirtualNodeType.Element)
        {
            ElementTag = tag,
            Properties = properties,
            Key = ExtractKey(properties),
            Reference = ExtractReference(properties),
            TextChildren = textChildren ?? string.Empty,
            ShapeFlag = ShapeFlags.Element | ShapeFlags.TextChildren,
            PatchFlag = patchFlag,
            DynamicProperties = dynamicProperties,
        };
    }

    /// <summary>
    /// Creates a compiler-shaped element vnode carrying patch hints (upstream:
    /// <c>createVNode(type, props, children, patchFlag, dynamicProps)</c>).
    /// </summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="properties">The properties, or null.</param>
    /// <param name="children">The children; null entries become comment placeholders.</param>
    /// <param name="patchFlag">The compiler patch hint.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> has <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode Element(
        string tag,
        VirtualNodeProperties? properties,
        VirtualNode?[]? children,
        PatchFlags patchFlag,
        string[]? dynamicProperties = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        var normalizedChildren = NormalizeArrayChildren(children);
        return new VirtualNode(VirtualNodeType.Element)
        {
            ElementTag = tag,
            Properties = properties,
            Key = ExtractKey(properties),
            Reference = ExtractReference(properties),
            ArrayChildren = normalizedChildren,
            ShapeFlag = normalizedChildren is null
                ? ShapeFlags.Element
                : ShapeFlags.Element | ShapeFlags.ArrayChildren,
            PatchFlag = patchFlag,
            DynamicProperties = dynamicProperties,
        };
    }

    /// <summary>Creates a text vnode (upstream: <c>createTextVNode</c>).</summary>
    /// <param name="content">The text content.</param>
    /// <param name="patchFlag">
    /// The compiler hint — <see cref="PatchFlags.Text"/> when the content is a dynamic expression.
    /// </param>
    public static VirtualNode Text(string content, PatchFlags patchFlag = default)
        => new(VirtualNodeType.Text)
        {
            TextChildren = content ?? string.Empty,
            PatchFlag = patchFlag,
        };

    /// <summary>Creates a comment vnode (upstream: <c>createCommentVNode</c>).</summary>
    /// <param name="content">The comment text.</param>
    public static VirtualNode Comment(string content = "")
        => new(VirtualNodeType.Comment)
        {
            TextChildren = content ?? string.Empty,
        };

    /// <summary>
    /// Creates a static vnode whose raw markup the platform inserts in a single operation
    /// (upstream: <c>createStaticVNode</c>). Requires the renderer's
    /// <c>InsertStaticContent</c> node-op.
    /// </summary>
    /// <param name="content">The raw markup.</param>
    public static VirtualNode Static(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new VirtualNode(VirtualNodeType.Static)
        {
            TextChildren = content,
        };
    }

    /// <summary>Creates a fragment vnode wrapping multiple root nodes.</summary>
    /// <param name="children">The children; null entries become comment placeholders.</param>
    public static VirtualNode Fragment(params VirtualNode?[]? children)
        => Fragment(children, null, default);

    /// <summary>Creates a keyed, optionally compiler-flagged fragment vnode.</summary>
    /// <param name="children">The children; null entries become comment placeholders.</param>
    /// <param name="key">The diffing key, or null.</param>
    /// <param name="patchFlag">
    /// The compiler hint (<see cref="PatchFlags.StableFragment"/>,
    /// <see cref="PatchFlags.KeyedFragment"/>, or <see cref="PatchFlags.UnkeyedFragment"/>).
    /// </param>
    public static VirtualNode Fragment(VirtualNode?[]? children, object? key, PatchFlags patchFlag = default)
        => new(VirtualNodeType.Fragment)
        {
            Key = key,
            ArrayChildren = NormalizeArrayChildren(children) ?? [],
            ShapeFlag = ShapeFlags.ArrayChildren,
            PatchFlag = patchFlag,
        };

    /// <summary>Builds a property bag from name/value tuples, pre-sized exactly.</summary>
    /// <param name="entries">The property entries.</param>
    /// <exception cref="ArgumentException">An entry name is null or empty.</exception>
    public static VirtualNodeProperties Properties(params (string Name, object? Value)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var properties = new VirtualNodeProperties(entries.Length);
        foreach (var (name, value) in entries)
        {
            properties.Set(name, value);
        }
        return properties;
    }

    /// <summary>Whether <paramref name="value"/> is a vnode (upstream: <c>isVNode</c>).</summary>
    /// <param name="value">The value to test.</param>
    public static bool IsVirtualNode(object? value) => value is VirtualNode;

    /// <summary>
    /// Clones <paramref name="node"/>, optionally merging <paramref name="extraProperties"/> per
    /// <see cref="MergeProperties"/> (upstream: <c>cloneVNode</c>). The clone shares children and
    /// copies the mount back-pointers — cloning is how an already-mounted cached/static vnode is
    /// reused without corrupting the original's <see cref="VirtualNode.El"/> (upstream's
    /// clone-on-reuse contract via <c>cloneIfMounted</c>).
    /// </summary>
    /// <param name="node">The vnode to clone.</param>
    /// <param name="extraProperties">Extra properties merged over the clone's, or null.</param>
    public static VirtualNode Clone(VirtualNode node, VirtualNodeProperties? extraProperties = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        var properties = extraProperties is null
            ? node.Properties
            : MergeProperties(node.Properties, extraProperties);
        return new VirtualNode(node.Type)
        {
            ElementTag = node.ElementTag,
            ComponentType = node.ComponentType,
            Properties = properties,
            Key = ExtractKey(properties) ?? node.Key,
            Reference = ExtractReference(properties) ?? node.Reference,
            TextChildren = node.TextChildren,
            ArrayChildren = node.ArrayChildren,
            SlotChildren = node.SlotChildren,
            ShapeFlag = node.ShapeFlag,
            PatchFlag = node.PatchFlag,
            DynamicProperties = node.DynamicProperties,
            DynamicChildren = node.DynamicChildren,
            El = node.El,
            Anchor = node.Anchor,
            Component = node.Component,
        };
    }

    /// <summary>
    /// Merges property bags left-to-right per Vue's rules (upstream: <c>mergeProps</c>,
    /// https://vuejs.org/api/render-function.html#mergeprops): <c>"class"</c> strings
    /// concatenate space-separated, <c>"style"</c> merges (strings join with <c>";"</c>;
    /// dictionaries merge later-wins), and <c>onX</c> event handlers chain into a multicast
    /// delegate so every handler is invoked. Everything else is later-wins. Class/style values
    /// beyond plain strings and dictionaries follow the normalization helpers when
    /// [V01.01.01.02] lands; until then non-string forms are later-wins.
    /// </summary>
    /// <param name="sources">The bags to merge; null entries are skipped.</param>
    /// <returns>A new bag; never null.</returns>
    public static VirtualNodeProperties MergeProperties(params VirtualNodeProperties?[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var merged = new VirtualNodeProperties();
        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }
            foreach (var (name, value) in source)
            {
                if (string.Equals(name, "class", StringComparison.Ordinal))
                {
                    merged.Set(name, MergeClassValues(merged[name], value));
                }
                else if (string.Equals(name, "style", StringComparison.Ordinal))
                {
                    merged.Set(name, MergeStyleValues(merged[name], value));
                }
                else if (IsEventListenerName(name)
                    && merged.TryGetValue(name, out var existing)
                    && existing is Delegate existingHandler
                    && value is Delegate incomingHandler
                    && !ReferenceEquals(existingHandler, incomingHandler))
                {
                    merged.Set(name, Delegate.Combine(existingHandler, incomingHandler));
                }
                else
                {
                    merged.Set(name, value);
                }
            }
        }
        return merged;
    }

    /// <summary>
    /// Normalizes a child slot per upstream <c>normalizeVNode</c>: null becomes a comment
    /// placeholder, and an already-mounted vnode is cloned so remounting cannot corrupt the
    /// original's <see cref="VirtualNode.El"/> (upstream: <c>cloneIfMounted</c>).
    /// </summary>
    /// <param name="node">The child, or null.</param>
    public static VirtualNode Normalize(VirtualNode? node)
    {
        if (node is null)
        {
            return Comment();
        }
        return node.El is null ? node : Clone(node);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is an event-listener prop: <c>on</c> followed by an
    /// upper-case letter (upstream: the <c>isOn</c> check in <c>@vue/shared</c>).
    /// </summary>
    /// <param name="name">The prop name.</param>
    public static bool IsEventListenerName(string name)
        => name is not null
            && name.Length > 2
            && name[0] == 'o'
            && name[1] == 'n'
            && char.IsAsciiLetterUpper(name[2]);

    private static VirtualNode[]? NormalizeArrayChildren(VirtualNode?[]? children)
    {
        if (children is null || children.Length == 0)
        {
            return null;
        }
        var normalized = new VirtualNode[children.Length];
        for (var index = 0; index < children.Length; index++)
        {
            normalized[index] = children[index] ?? Comment();
        }
        return normalized;
    }

    private static object? ExtractKey(VirtualNodeProperties? properties)
        => properties is not null && properties.TryGetValue("key", out var key) ? key : null;

    private static object? ExtractReference(VirtualNodeProperties? properties)
        => properties is not null && properties.TryGetValue("ref", out var reference) ? reference : null;

    private static object? MergeClassValues(object? existing, object? incoming)
    {
        if (existing is string existingText && !string.IsNullOrEmpty(existingText)
            && incoming is string incomingText && !string.IsNullOrEmpty(incomingText))
        {
            return existingText + " " + incomingText;
        }
        return incoming ?? existing;
    }

    private static object? MergeStyleValues(object? existing, object? incoming)
    {
        if (existing is string existingText && !string.IsNullOrEmpty(existingText)
            && incoming is string incomingText && !string.IsNullOrEmpty(incomingText))
        {
            return existingText + ";" + incomingText;
        }
        if (existing is IReadOnlyDictionary<string, object?> existingMap
            && incoming is IReadOnlyDictionary<string, object?> incomingMap)
        {
            var mergedMap = new Dictionary<string, object?>(
                existingMap.Count + incomingMap.Count,
                StringComparer.Ordinal);
            foreach (var (property, propertyValue) in existingMap)
            {
                mergedMap[property] = propertyValue;
            }
            foreach (var (property, propertyValue) in incomingMap)
            {
                mergedMap[property] = propertyValue;
            }
            return mergedMap;
        }
        return incoming ?? existing;
    }
}
