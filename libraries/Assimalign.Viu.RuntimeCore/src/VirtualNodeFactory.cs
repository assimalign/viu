using System;
using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeCore;

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
        var vnode = BuildElement(tag, properties, textChildren, patchFlag, dynamicProperties);
        BlockStack.TrackDynamicChild(vnode);
        return vnode;
    }

    private static VirtualNode BuildElement(
        string tag,
        VirtualNodeProperties? properties,
        string textChildren,
        PatchFlags patchFlag,
        string[]? dynamicProperties)
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
        var vnode = BuildElement(tag, properties, children, patchFlag, dynamicProperties);
        BlockStack.TrackDynamicChild(vnode);
        return vnode;
    }

    private static VirtualNode BuildElement(
        string tag,
        VirtualNodeProperties? properties,
        VirtualNode?[]? children,
        PatchFlags patchFlag,
        string[]? dynamicProperties)
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

    /// <summary>
    /// Opens a block so the vnodes created until the matching block factory call are collected as
    /// its dynamic descendants (upstream: <c>openBlock</c>,
    /// https://vuejs.org/guide/extras/rendering-mechanism.html#compiler-informed-virtual-dom). Pair
    /// every call with a block factory — <see cref="ElementBlock(string, VirtualNodeProperties?, VirtualNode?[]?, PatchFlags, string[]?)"/>
    /// or <see cref="FragmentBlock"/> — that closes it. Not thread-safe (single-threaded JS
    /// event-loop model).
    /// </summary>
    /// <param name="disableTracking">
    /// True for v-once content whose descendants are created once and never collected.
    /// </param>
    public static void OpenBlock(bool disableTracking = false) => BlockStack.OpenBlock(disableTracking);

    /// <summary>
    /// Suspends or resumes block-tree tracking (upstream: <c>setBlockTracking</c>): a v-once
    /// expression brackets its cached content with <c>SetBlockTracking(-1, inVOnce: true)</c> then
    /// <c>SetBlockTracking(1)</c> so the content is not collected as a dynamic child. Passing
    /// <paramref name="inVOnce"/> on the suspending call also marks the enclosing block so unmount
    /// skips the fast path and still tears down components nested in the v-once content
    /// (upstream #5154; see <see cref="VirtualNode.HasOnce"/>).
    /// </summary>
    /// <param name="value">-1 suspends collection; +1 resumes it.</param>
    /// <param name="inVOnce">True when the suspension brackets v-once content (marks the block).</param>
    public static void SetBlockTracking(int value, bool inVOnce = false) => BlockStack.SetBlockTracking(value, inVOnce);

    /// <summary>
    /// Creates a block element vnode whose <see cref="VirtualNode.DynamicChildren"/> are the dynamic
    /// descendants collected since <see cref="OpenBlock"/> (upstream: <c>createElementBlock</c>); the
    /// renderer patches only those descendants on update, skipping the static subtree.
    /// </summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="properties">The properties, or null.</param>
    /// <param name="children">The children; null entries become comment placeholders.</param>
    /// <param name="patchFlag">The compiler patch hint for this element's own props.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> has <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode ElementBlock(
        string tag,
        VirtualNodeProperties? properties,
        VirtualNode?[]? children,
        PatchFlags patchFlag = default,
        string[]? dynamicProperties = null)
        => BlockStack.CloseBlockAndSetup(BuildElement(tag, properties, children, patchFlag, dynamicProperties));

    /// <summary>
    /// Creates a block element vnode with text children whose
    /// <see cref="VirtualNode.DynamicChildren"/> are the dynamic descendants collected since
    /// <see cref="OpenBlock"/> (upstream: <c>createElementBlock</c>).
    /// </summary>
    /// <param name="tag">The element tag.</param>
    /// <param name="properties">The properties, or null.</param>
    /// <param name="textChildren">The text content.</param>
    /// <param name="patchFlag">The compiler patch hint for this element's own props.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> has <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode ElementBlock(
        string tag,
        VirtualNodeProperties? properties,
        string textChildren,
        PatchFlags patchFlag = default,
        string[]? dynamicProperties = null)
        => BlockStack.CloseBlockAndSetup(BuildElement(tag, properties, textChildren, patchFlag, dynamicProperties));

    /// <summary>Creates a text vnode (upstream: <c>createTextVNode</c>).</summary>
    /// <param name="content">The text content.</param>
    /// <param name="patchFlag">
    /// The compiler hint — <see cref="PatchFlags.Text"/> when the content is a dynamic expression.
    /// </param>
    public static VirtualNode Text(string content, PatchFlags patchFlag = default)
    {
        var vnode = new VirtualNode(VirtualNodeType.Text)
        {
            TextChildren = content ?? string.Empty,
            PatchFlag = patchFlag,
        };
        // Dynamic text (PatchFlags.Text) is a collected block child (upstream createTextVNode).
        BlockStack.TrackDynamicChild(vnode);
        return vnode;
    }

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

    /// <summary>
    /// Creates a component vnode (upstream: <c>createVNode(Component, props)</c>). The
    /// renderer instantiates a <see cref="ComponentInstance"/> for it on mount; declared props
    /// resolve from <paramref name="properties"/> and the rest falls through as attrs
    /// ([V01.01.03.07]).
    /// </summary>
    /// <param name="definition">The component definition.</param>
    /// <param name="properties">The props passed by the parent, or null.</param>
    public static VirtualNode Component(IComponentDefinition definition, VirtualNodeProperties? properties = null)
        => Component(definition, properties, default, null);

    /// <summary>
    /// Creates a compiler-shaped component vnode carrying patch hints: with
    /// <see cref="PatchFlags.Props"/> and <paramref name="dynamicProperties"/>, the parent
    /// update compares only the listed props to decide whether the child re-renders
    /// (upstream: <c>shouldUpdateComponent</c>'s optimized path).
    /// </summary>
    /// <param name="definition">The component definition.</param>
    /// <param name="properties">The props passed by the parent, or null.</param>
    /// <param name="patchFlag">The compiler patch hint.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> has <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode Component(
        IComponentDefinition definition,
        VirtualNodeProperties? properties,
        PatchFlags patchFlag,
        string[]? dynamicProperties)
    {
        var vnode = BuildComponent(definition, properties, null, patchFlag, dynamicProperties);
        // A component is always collected into the enclosing block (upstream: shapeFlag & COMPONENT):
        // it must persist its instance to the next vnode even when its own props do not change.
        BlockStack.TrackDynamicChild(vnode);
        return vnode;
    }

    /// <summary>
    /// Creates a component vnode carrying <paramref name="slots"/> (upstream:
    /// <c>createVNode(Component, props, slots)</c>). The instance renders named slots through
    /// <see cref="RenderSlot"/>. A <see cref="SlotFlags.Forwarded"/> slots object is resolved to
    /// <see cref="SlotFlags.Stable"/> or <see cref="SlotFlags.Dynamic"/> here against the forwarding
    /// component's own slot stability — exactly where upstream <c>normalizeChildren</c> resolves it,
    /// using <see cref="ComponentInstance.Current"/> as the forwarding (currently rendering)
    /// instance.
    /// </summary>
    /// <param name="definition">The component definition.</param>
    /// <param name="properties">The props passed by the parent, or null.</param>
    /// <param name="slots">The slot content, or null.</param>
    /// <param name="patchFlag">The compiler patch hint (<see cref="PatchFlags.DynamicSlots"/> forces child updates).</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> has <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode Component(
        IComponentDefinition definition,
        VirtualNodeProperties? properties,
        ComponentSlots? slots,
        PatchFlags patchFlag = default,
        string[]? dynamicProperties = null)
    {
        var vnode = BuildComponent(definition, properties, slots, patchFlag, dynamicProperties);
        // A component is always collected into the enclosing block (upstream: shapeFlag & COMPONENT).
        BlockStack.TrackDynamicChild(vnode);
        return vnode;
    }

    /// <summary>
    /// Creates a block component vnode whose <see cref="VirtualNode.DynamicChildren"/> are the dynamic
    /// descendants collected since <see cref="OpenBlock"/> (upstream: <c>createBlock</c> over a component
    /// type, i.e. <c>setupBlock(createVNode(Component, ...))</c>). This is the block-form counterpart of
    /// <see cref="Component(IComponentDefinition, VirtualNodeProperties?, ComponentSlots?, PatchFlags, string[]?)"/>
    /// that the compiled render emits as <c>createBlock</c>; it closes the open block onto the component
    /// vnode instead of tracking the vnode into that same block.
    /// </summary>
    /// <param name="definition">The component definition.</param>
    /// <param name="properties">The props passed by the parent, or null.</param>
    /// <param name="slots">The slot content, or null.</param>
    /// <param name="patchFlag">The compiler patch hint.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> has <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode ComponentBlock(
        IComponentDefinition definition,
        VirtualNodeProperties? properties,
        ComponentSlots? slots,
        PatchFlags patchFlag = default,
        string[]? dynamicProperties = null)
        => BlockStack.CloseBlockAndSetup(BuildComponent(definition, properties, slots, patchFlag, dynamicProperties));

    private static VirtualNode BuildComponent(
        IComponentDefinition definition,
        VirtualNodeProperties? properties,
        ComponentSlots? slots,
        PatchFlags patchFlag,
        string[]? dynamicProperties)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (slots is not null && slots.Flag == SlotFlags.Forwarded)
        {
            var forwardingSlots = ComponentInstance.Current?.Slots;
            slots.Flag = forwardingSlots is not null && forwardingSlots.Flag == SlotFlags.Stable
                ? SlotFlags.Stable
                : SlotFlags.Dynamic;
        }
        return new VirtualNode(VirtualNodeType.Component)
        {
            ComponentType = definition,
            Properties = properties,
            Key = ExtractKey(properties),
            Reference = ExtractReference(properties),
            SlotChildren = slots,
            ShapeFlag = slots is null
                ? ShapeFlags.StatefulComponent
                : ShapeFlags.StatefulComponent | ShapeFlags.SlotsChildren,
            PatchFlag = patchFlag,
            DynamicProperties = dynamicProperties,
        };
    }

    /// <summary>
    /// Renders the named slot as a fragment (upstream: <c>renderSlot</c>,
    /// <c>packages/runtime-core/src/helpers/renderSlot.ts</c>). Invokes the slot with
    /// <paramref name="properties"/> (the scoped-slot scope); <paramref name="fallback"/>'s content
    /// renders when the slot is absent <b>or</b> when its content renders empty — upstream's
    /// <c>ensureValidVNode</c> treats comment-only and empty output as no content, and a null entry
    /// is this model's comment-placeholder idiom. The result is a keyed fragment so it patches in
    /// place across re-renders; the fallback branch takes a distinct <c>_fb</c>-suffixed key
    /// (upstream parity) so slot content and fallback content never patch against each other.
    /// </summary>
    /// <param name="slots">The instance's slots (<see cref="ComponentInstance.Slots"/>), or null.</param>
    /// <param name="name">The slot name (<c>"default"</c> for the default slot).</param>
    /// <param name="properties">The scoped-slot props to pass, or null.</param>
    /// <param name="fallback">The fallback content factory, or null.</param>
    /// <returns>A fragment vnode wrapping the slot's (or fallback's) vnodes.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public static VirtualNode RenderSlot(
        ComponentSlots? slots,
        string name,
        object? properties = null,
        Func<VirtualNode?[]?>? fallback = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        // The slot delegate captures its defining (parent) render context; invoking it here runs
        // that content as part of the child's subtree. Reactive reads therefore attribute to the
        // child's render effect (upstream withCtx parity — reactive tracking stays live; only the
        // block-tracking suppression is deferred to the block-tree work [V01.01.03.15]).
        VirtualNode?[]? children = null;
        if (slots is not null && slots.TryGetSlot(name, out var slot))
        {
            children = slot(properties);
        }
        if (fallback is null || HasRenderableContent(children))
        {
            return Fragment(children, "_" + name);
        }
        return Fragment(fallback(), "_" + name + "_fb");
    }

    private static bool HasRenderableContent(VirtualNode?[]? children)
    {
        // Upstream ensureValidVNode: slot output counts as content only if some child is neither a
        // comment nor a fragment that itself renders empty. Null entries are this model's
        // comment-placeholder idiom (see Fragment), so they count as empty too.
        if (children is null)
        {
            return false;
        }
        for (var index = 0; index < children.Length; index++)
        {
            var child = children[index];
            if (child is null || child.Type == VirtualNodeType.Comment)
            {
                continue;
            }
            if (child.Type == VirtualNodeType.Fragment && !HasRenderableContent(child.ArrayChildren))
            {
                continue;
            }
            return true;
        }
        return false;
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
    {
        var vnode = BuildFragment(children, key, patchFlag);
        // A flagged fragment (keyed/unkeyed/stable v-for) is a collected block child; a plain
        // fragment (patchFlag 0, e.g. RenderSlot's output) is not, keeping slot behavior identical.
        BlockStack.TrackDynamicChild(vnode);
        return vnode;
    }

    /// <summary>
    /// Creates a block fragment vnode whose <see cref="VirtualNode.DynamicChildren"/> are the
    /// dynamic descendants collected since <see cref="OpenBlock"/> (upstream: <c>createBlock</c> over
    /// the <c>Fragment</c> type). A stable fragment with a block tree patches only those descendants
    /// positionally, never through the keyed diff.
    /// </summary>
    /// <param name="children">The children; null entries become comment placeholders.</param>
    /// <param name="key">The diffing key, or null.</param>
    /// <param name="patchFlag">The compiler hint (typically <see cref="PatchFlags.StableFragment"/>).</param>
    public static VirtualNode FragmentBlock(VirtualNode?[]? children, object? key = null, PatchFlags patchFlag = default)
        => BlockStack.CloseBlockAndSetup(BuildFragment(children, key, patchFlag));

    private static VirtualNode BuildFragment(VirtualNode?[]? children, object? key, PatchFlags patchFlag)
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
            HasOnce = node.HasOnce,
            Directives = node.Directives,
            // Carry the transition hooks across a clone (upstream cloneVNode copies vnode.transition):
            // a reused/normalized child keeps its enter/leave choreography.
            Transition = node.Transition,
            AppContext = node.AppContext,
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

    private static TemplateReference? ExtractReference(VirtualNodeProperties? properties)
        => properties is not null && properties.TryGetValue("ref", out var reference)
            ? TemplateReference.FromRaw(reference)
            : null;

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
