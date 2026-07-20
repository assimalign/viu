using System;
using System.Collections.Generic;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The runtime half of the by-name helper contract the render-function code generator emits against —
/// the C# port of the render-helper exports of <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/vnode.ts</c>, <c>helpers/renderList.ts</c>, <c>helpers/renderSlot.ts</c>,
/// <c>helpers/resolveAssets.ts</c>, <c>componentRenderContext.ts</c>, and <c>@vue/shared</c>
/// <c>toDisplayString</c>). Generated render bodies bind every helper through a single
/// <c>using static global::Assimalign.Viu.RuntimeCore.RenderHelpers;</c> — the C# analogue of upstream's
/// aliased helper import — so the members here carry the upstream-aliased spellings (<c>_openBlock</c>,
/// <c>_createElementBlock</c>, …), the same names the emitter writes. The authoritative name/signature table
/// is <c>Assimalign.Viu.Syntax.Templates/docs/DESIGN.md</c>; this surface satisfies every call shape the
/// emitter (<c>Internal/RenderCodeWriter.cs</c>) can produce.
/// <para>
/// The <c>_</c>-prefixed lowercase member names are a deliberate, generated-code-only deviation from the
/// repository whole-word C# naming rule (<c>.claude/rules/general-rules.md</c>) — the names <b>are</b> the
/// upstream contract, and the compiler binds them literally. Deviates from the general-rules naming rule per
/// design decision: the render-helper names are the upstream <c>helperNameMap</c> contract, pinned by the
/// Templates <c>docs/DESIGN.md</c> table and <c>RenderFunctionEmitterTests</c>.
/// </para>
/// <para>
/// This class references only <c>Assimalign.Viu.RuntimeCore</c>, <c>Assimalign.Viu.Reactivity</c>, and
/// <c>Assimalign.Viu.Shared</c> — never any <c>Assimalign.Viu.Syntax.*</c> assembly. The contract flows one
/// way (by name) so the runtime never depends on the compiler. DOM-only helpers (<c>_vShow</c>, the
/// <c>_vModel*</c> directive values, <c>_withModifiers</c>/<c>_withKeys</c>, <c>_Transition</c>/
/// <c>_TransitionGroup</c>) are <b>not</b> here: their behavior lives in <c>Assimalign.Viu.RuntimeDom</c>,
/// which this platform-agnostic layer must not reference — see <c>docs/DESIGN.md</c> for that split.
/// Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
public static class RenderHelpers
{
    // ==== Block open / tracking (upstream openBlock / setBlockTracking, vnode.ts) ================

    /// <summary>
    /// Opens an optimization block so vnodes created until the matching block factory are collected as its
    /// dynamic descendants (upstream: <c>openBlock</c>). Returns the opaque <see cref="BlockToken"/> the
    /// emitter threads as the first argument of <see cref="_createElementBlock"/>/<see cref="_createBlock"/>
    /// to sequence the open before any child argument is evaluated (C# has no comma operator).
    /// </summary>
    /// <param name="disableTracking">True for <c>v-for</c> fragments whose children come from the render list, not the block tree.</param>
    /// <returns>The evaluation-order token.</returns>
    public static BlockToken _openBlock(bool disableTracking = false)
    {
        VirtualNodeFactory.OpenBlock(disableTracking);
        return default;
    }

    /// <summary>
    /// Suspends (<paramref name="value"/> &lt; 0) or resumes (&gt; 0) block-tree collection (upstream:
    /// <c>setBlockTracking</c>); a <c>v-once</c> subtree brackets its cached content so the descendants are
    /// created once and never collected. Returns the token <see cref="_setCache"/> uses to invert the
    /// suspension. When <paramref name="inVOnce"/> is set on the suspending call, the enclosing block is
    /// marked so unmount still tears down components nested in the cached content (upstream #5154).
    /// </summary>
    /// <param name="value">-1 suspends collection; +1 resumes it.</param>
    /// <param name="inVOnce">True when the suspension brackets <c>v-once</c> content.</param>
    /// <returns>A token recording the applied delta, accepted by <see cref="_setCache"/>.</returns>
    public static BlockToken _setBlockTracking(int value, bool inVOnce = false)
    {
        VirtualNodeFactory.SetBlockTracking(value, inVOnce);
        return new BlockToken(value);
    }

    // ==== VNode / block factories (upstream createVNode / createElementBlock / createBlock) ======

    /// <summary>
    /// Closes the open block onto an element or fragment vnode carrying patch hints (upstream:
    /// <c>createElementBlock</c>). The block opened by the <paramref name="block"/> argument's
    /// <see cref="_openBlock"/> is collected onto the result's <see cref="VirtualNode.DynamicChildren"/>.
    /// </summary>
    /// <param name="block">The token from <see cref="_openBlock"/> (sequences the open; value unused).</param>
    /// <param name="tag">An element tag string or <see cref="_Fragment"/>.</param>
    /// <param name="properties">The props (from <see cref="_createProps"/>), or null.</param>
    /// <param name="children">Text, a single vnode, or a vnode/object array.</param>
    /// <param name="patchFlag">The compiler patch hint (numeric parity with <see cref="PatchFlags"/>).</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> carries <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode _createElementBlock(
        BlockToken block,
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
        => CreateBaseVNode(tag, properties, children, patchFlag, dynamicProperties, asBlock: true);

    /// <summary>
    /// Closes the open block onto a component, dynamic-component, or built-in vnode (upstream:
    /// <c>createBlock</c>). Dispatches on <paramref name="tag"/>: an <see cref="IComponentDefinition"/> is a
    /// component, a string is an element (dynamic-component element fallback), <see cref="_Fragment"/> is a
    /// fragment, and null renders a comment placeholder.
    /// </summary>
    /// <param name="block">The token from <see cref="_openBlock"/> (sequences the open; value unused).</param>
    /// <param name="tag">A component definition, an element tag string, a built-in, or null.</param>
    /// <param name="properties">The props (from <see cref="_createProps"/>), or null.</param>
    /// <param name="children">Component slots (from <see cref="_createProps"/>), text, or a vnode array.</param>
    /// <param name="patchFlag">The compiler patch hint.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> carries <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode _createBlock(
        BlockToken block,
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
        => CreateBaseVNode(tag, properties, children, patchFlag, dynamicProperties, asBlock: true);

    /// <summary>
    /// Creates a plain (non-block) element or fragment vnode carrying patch hints (upstream:
    /// <c>createElementVNode</c>/<c>createBaseVNode</c>). Collected into the enclosing block by patch flag.
    /// </summary>
    /// <param name="tag">An element tag string or <see cref="_Fragment"/>.</param>
    /// <param name="properties">The props (from <see cref="_createProps"/>), or null.</param>
    /// <param name="children">Text, a single vnode, or a vnode/object array.</param>
    /// <param name="patchFlag">The compiler patch hint.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> carries <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode _createElementVNode(
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
        => CreateBaseVNode(tag, properties, children, patchFlag, dynamicProperties, asBlock: false);

    /// <summary>
    /// Creates a plain (non-block) component or element vnode carrying patch hints (upstream:
    /// <c>createVNode</c>). Dispatches on <paramref name="tag"/> exactly as <see cref="_createBlock"/> does.
    /// </summary>
    /// <param name="tag">A component definition, an element tag string, a built-in, or null.</param>
    /// <param name="properties">The props (from <see cref="_createProps"/>), or null.</param>
    /// <param name="children">Component slots, text, a single vnode, or a vnode array.</param>
    /// <param name="patchFlag">The compiler patch hint.</param>
    /// <param name="dynamicProperties">The dynamic prop names when <paramref name="patchFlag"/> carries <see cref="PatchFlags.Props"/>.</param>
    public static VirtualNode _createVNode(
        object? tag,
        object? properties = null,
        object? children = null,
        int patchFlag = 0,
        string[]? dynamicProperties = null)
        => CreateBaseVNode(tag, properties, children, patchFlag, dynamicProperties, asBlock: false);

    /// <summary>Creates a text vnode (upstream: <c>createTextVNode</c>).</summary>
    /// <param name="text">The text payload; coerced with <see cref="DisplayStringFormatter"/> when non-string.</param>
    /// <param name="patchFlag">The compiler hint (<see cref="PatchFlags.Text"/> for dynamic text).</param>
    public static VirtualNode _createTextVNode(object? text = null, int patchFlag = 0)
        => VirtualNodeFactory.Text(CoerceText(text), (PatchFlags)patchFlag);

    /// <summary>
    /// Creates a comment vnode (upstream: <c>createCommentVNode</c>). With <paramref name="asBlock"/> the
    /// comment is wrapped in its own block — the <c>v-if</c>/<c>v-else</c> chain terminator emits
    /// <c>_createCommentVNode("v-if", true)</c> so the else branch patches consistently against the
    /// block-form branches.
    /// </summary>
    /// <param name="text">The comment text.</param>
    /// <param name="asBlock">True to open and close a block around the comment.</param>
    public static VirtualNode _createCommentVNode(string? text = "", bool asBlock = false)
    {
        if (!asBlock)
        {
            return VirtualNodeFactory.Comment(text ?? string.Empty);
        }
        // Upstream: asBlock ? (openBlock(), createBlock(Comment, null, text)) : createVNode(Comment,...).
        // The emitter writes _createCommentVNode(text, true) with no separate _openBlock, so the block is
        // opened and closed here.
        VirtualNodeFactory.OpenBlock();
        return BlockStack.CloseBlockAndSetup(VirtualNodeFactory.Comment(text ?? string.Empty));
    }

    /// <summary>
    /// Creates a static vnode whose raw markup the platform inserts in one operation (upstream:
    /// <c>createStaticVNode</c>). Requires the renderer's <c>InsertStaticContent</c> node op.
    /// </summary>
    /// <param name="content">The raw markup.</param>
    /// <param name="count">
    /// The number of top-level nodes in <paramref name="content"/> (upstream hint for anchor tracking);
    /// accepted for contract parity — this model derives the range from the inserted nodes, so the count is
    /// unused.
    /// </param>
    public static VirtualNode _createStaticVNode(string content, int count) => VirtualNodeFactory.Static(content);

    // ==== Interpolation (upstream @vue/shared toDisplayString) ===================================

    /// <summary>Stringifies an interpolation value (upstream: <c>toDisplayString</c>).</summary>
    /// <param name="value">The interpolated value.</param>
    /// <returns>The display string; never null.</returns>
    public static string _toDisplayString(object? value) => DisplayStringFormatter.ToDisplayString(value);

    // ==== v-for (upstream helpers/renderList.ts) =================================================

    /// <summary>
    /// Renders a list for <c>v-for</c> (upstream: <c>renderList</c> over an iterable). The generic parameter
    /// gives the emitted <c>(item) =&gt; …</c> lambda its item type.
    /// </summary>
    /// <typeparam name="T">The item type, inferred from <paramref name="source"/>.</typeparam>
    /// <param name="source">The iterated collection, or null.</param>
    /// <param name="render">Produces one vnode per item.</param>
    /// <returns>The per-item vnodes, in order.</returns>
    public static VirtualNode?[] _renderList<T>(IEnumerable<T>? source, Func<T, VirtualNode?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (source is null)
        {
            return [];
        }
        var result = new List<VirtualNode?>(source is ICollection<T> collection ? collection.Count : 4);
        foreach (var item in source)
        {
            result.Add(render(item));
        }
        return result.ToArray();
    }

    /// <summary>
    /// Renders a list for <c>v-for</c> with the item index (upstream: <c>renderList</c>'s <c>(item, index)</c>
    /// arm). The generics give the emitted <c>(item, index) =&gt; …</c> lambda its parameter types.
    /// </summary>
    /// <typeparam name="T">The item type, inferred from <paramref name="source"/>.</typeparam>
    /// <param name="source">The iterated collection, or null.</param>
    /// <param name="render">Produces one vnode per item and its zero-based index.</param>
    /// <returns>The per-item vnodes, in order.</returns>
    public static VirtualNode?[] _renderList<T>(IEnumerable<T>? source, Func<T, int, VirtualNode?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (source is null)
        {
            return [];
        }
        var result = new List<VirtualNode?>(source is ICollection<T> collection ? collection.Count : 4);
        var index = 0;
        foreach (var item in source)
        {
            result.Add(render(item, index++));
        }
        return result.ToArray();
    }

    /// <summary>
    /// Renders a numeric range for <c>v-for="n in count"</c> (upstream: <c>renderList</c>'s number arm,
    /// one-based: <c>n</c> runs 1..<paramref name="count"/>).
    /// </summary>
    /// <param name="count">The upper bound (inclusive, one-based).</param>
    /// <param name="render">Produces one vnode per number.</param>
    /// <returns>The per-number vnodes.</returns>
    public static VirtualNode?[] _renderList(int count, Func<int, VirtualNode?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        var result = new VirtualNode?[count < 0 ? 0 : count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = render(index + 1);
        }
        return result;
    }

    /// <summary>
    /// Renders a numeric range for <c>v-for="(n, index) in count"</c> (upstream: <c>renderList</c>'s number
    /// arm with index; <c>n</c> one-based, <c>index</c> zero-based).
    /// </summary>
    /// <param name="count">The upper bound (inclusive, one-based).</param>
    /// <param name="render">Produces one vnode per number and its zero-based index.</param>
    /// <returns>The per-number vnodes.</returns>
    public static VirtualNode?[] _renderList(int count, Func<int, int, VirtualNode?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        var result = new VirtualNode?[count < 0 ? 0 : count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = render(index + 1, index);
        }
        return result;
    }

    /// <summary>
    /// Renders an object's entries for <c>v-for="(value, key, index) in object"</c> (upstream:
    /// <c>renderList</c>'s object arm). The generics give the emitted lambda its <c>(value, key, index)</c>
    /// parameter types.
    /// </summary>
    /// <typeparam name="TKey">The entry key type.</typeparam>
    /// <typeparam name="TValue">The entry value type.</typeparam>
    /// <param name="source">The key/value entries, or null.</param>
    /// <param name="render">Produces one vnode per value, key, and zero-based index.</param>
    /// <returns>The per-entry vnodes, in order.</returns>
    public static VirtualNode?[] _renderList<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? source,
        Func<TValue, TKey, int, VirtualNode?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (source is null)
        {
            return [];
        }
        var result = new List<VirtualNode?>(source is ICollection<KeyValuePair<TKey, TValue>> collection ? collection.Count : 4);
        var index = 0;
        foreach (var entry in source)
        {
            result.Add(render(entry.Value, entry.Key, index++));
        }
        return result.ToArray();
    }

    // ==== Slots (upstream helpers/renderSlot.ts, componentRenderContext.ts) ======================

    /// <summary>
    /// Renders a slot outlet (upstream: <c>renderSlot</c>). The compiled render passes the instance's slots
    /// (spelled <c>_ctx.__slots</c> — <c>$</c> is not legal in C#), the outlet name, the scoped-slot props,
    /// and an optional fallback factory.
    /// </summary>
    /// <param name="slots">The instance's slots (<see cref="ComponentInstance.Slots"/>), or null.</param>
    /// <param name="name">The slot name.</param>
    /// <param name="properties">The scoped-slot props to pass, or null.</param>
    /// <param name="fallback">The fallback content factory, or null.</param>
    public static VirtualNode _renderSlot(
        ComponentSlots? slots,
        string name,
        object? properties = null,
        Func<object?[]?>? fallback = null)
    {
        Func<VirtualNode?[]?>? adapted = fallback is null ? null : () => CoerceVirtualNodeArray(fallback());
        return VirtualNodeFactory.RenderSlot(slots, name, properties, adapted);
    }

    /// <summary>
    /// Wraps a non-scoped slot function with the render context (upstream: <c>withCtx</c>). The emitted
    /// <c>() =&gt; new object?[] { … }</c> slot body is target-typed by this overload.
    /// </summary>
    /// <param name="render">The slot body producing its vnodes.</param>
    /// <returns>The wrapped <see cref="Slot"/>.</returns>
    public static Slot _withCtx(Func<object?[]?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return _ => CoerceVirtualNodeArray(render());
    }

    /// <summary>
    /// Wraps a scoped slot function with the render context (upstream: <c>withCtx</c>). The emitted
    /// <c>(slotProps) =&gt; new object?[] { … }</c> slot body is target-typed by this overload.
    /// </summary>
    /// <param name="render">The slot body producing its vnodes from the child-supplied scope.</param>
    /// <returns>The wrapped <see cref="Slot"/>.</returns>
    public static Slot _withCtx(Func<object?, object?[]?> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return properties => CoerceVirtualNodeArray(render(properties));
    }

    // ==== Asset resolution (upstream helpers/resolveAssets.ts) ===================================

    /// <summary>
    /// Resolves a component by name against the current instance's app registry (upstream:
    /// <c>resolveComponent</c>). Falls back to the raw <paramref name="name"/> (used as a late-resolved tag)
    /// when unregistered, matching upstream's <c>resolveAsset</c> fallback.
    /// </summary>
    /// <param name="name">The component name.</param>
    /// <returns>The resolved <see cref="IComponentDefinition"/>, or the name for element/late fallback.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public static object? _resolveComponent(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var resolved = ComponentInstance.Current?.AppContext?.ResolveComponent(name);
        if (resolved is not null)
        {
            return resolved;
        }
        RuntimeWarnings.Warn($"Failed to resolve component: {name}");
        return name;
    }

    /// <summary>Resolves a directive by name (upstream: <c>resolveDirective</c>).</summary>
    /// <param name="name">The directive name.</param>
    /// <returns>The resolved directive, or null.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public static IDirective? _resolveDirective(string name) => Directives.ResolveDirective(name);

    /// <summary>Resolves a <c>&lt;component :is&gt;</c> value (upstream: <c>resolveDynamicComponent</c>).</summary>
    /// <param name="value">The <c>is</c> value — a component definition, a name, or null.</param>
    /// <returns>The resolved component, the element-tag string, or null.</returns>
    public static object? _resolveDynamicComponent(object? value) => DynamicComponents.ResolveDynamicComponent(value);

    // ==== Runtime directives (upstream directives.ts withDirectives) =============================

    /// <summary>
    /// Applies runtime directives to a vnode (upstream: <c>withDirectives(vnode, [[dir, value, arg,
    /// modifiers], …])</c>). Each entry of <paramref name="directives"/> is itself an
    /// <c>object?[]</c> of <c>[directive, value?, argument?, modifiers?]</c> — the shape the emitter writes
    /// as <c>new object?[] { new object?[] { dir, value } }</c>.
    /// </summary>
    /// <param name="vnode">The vnode to bind directives to.</param>
    /// <param name="directives">The directive tuples.</param>
    /// <returns><paramref name="vnode"/>, for chaining.</returns>
    public static VirtualNode _withDirectives(VirtualNode vnode, object?[] directives)
    {
        ArgumentNullException.ThrowIfNull(directives);
        var arguments = new DirectiveArgument[directives.Length];
        var count = 0;
        foreach (var entry in directives)
        {
            if (entry is object?[] tuple && tuple.Length > 0 && tuple[0] is IDirective directive)
            {
                arguments[count++] = new DirectiveArgument(
                    directive,
                    tuple.Length > 1 ? tuple[1] : null,
                    tuple.Length > 2 ? tuple[2] as string : null,
                    tuple.Length > 3 ? tuple[3] as IReadOnlyDictionary<string, bool> : null);
            }
        }
        if (count != arguments.Length)
        {
            Array.Resize(ref arguments, count);
        }
        return Directives.WithDirectives(vnode, arguments);
    }

    // ==== Prop normalization (upstream mergeProps / normalize* / vShared casings) ================

    /// <summary>Merges several props sources left-to-right per Vue's rules (upstream: <c>mergeProps</c>).</summary>
    /// <param name="sources">The props sources (from <see cref="_createProps"/> or bindings); non-bag entries are skipped.</param>
    /// <returns>The merged bag; never null.</returns>
    public static VirtualNodeProperties _mergeProps(params object?[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var bags = new VirtualNodeProperties?[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            bags[index] = sources[index] as VirtualNodeProperties;
        }
        return VirtualNodeFactory.MergeProperties(bags);
    }

    /// <summary>Normalizes a dynamic <c>class</c> binding to a class string (upstream: <c>normalizeClass</c>).</summary>
    /// <param name="value">The class value (string, list, or name/flag map).</param>
    public static string _normalizeClass(object? value) => StyleAndClassNormalization.NormalizeClass(value);

    /// <summary>Normalizes a dynamic <c>style</c> binding (upstream: <c>normalizeStyle</c>).</summary>
    /// <param name="value">The style value (string, list, or property map).</param>
    public static object? _normalizeStyle(object? value) => StyleAndClassNormalization.NormalizeStyle(value);

    /// <summary>
    /// Normalizes a props object's <c>class</c>/<c>style</c> entries in place (upstream:
    /// <c>normalizeProps</c>), returning it unchanged when it is not a props bag.
    /// </summary>
    /// <param name="properties">The props object.</param>
    public static object? _normalizeProps(object? properties)
    {
        if (properties is VirtualNodeProperties bag)
        {
            if (bag.TryGetValue("class", out var cssClass) && cssClass is not null)
            {
                bag.Set("class", StyleAndClassNormalization.NormalizeClass(cssClass));
            }
            if (bag.TryGetValue("style", out var style) && style is not null)
            {
                bag.Set("style", StyleAndClassNormalization.NormalizeStyle(style));
            }
        }
        return properties;
    }

    /// <summary>
    /// Guards a reactive props object before it is spread onto a vnode (upstream:
    /// <c>guardReactiveProps</c>). Viu prop bags are not identity-swapping proxies, so this returns the
    /// argument unchanged (documented C# divergence: there is no proxy to clone away).
    /// </summary>
    /// <param name="properties">The props object.</param>
    public static object? _guardReactiveProps(object? properties) => properties;

    /// <summary>
    /// Converts a <c>v-on="{ event: handler }"</c> map to on-prefixed handler props (upstream:
    /// <c>toHandlers</c>).
    /// </summary>
    /// <param name="value">The handlers map (a props bag), or null.</param>
    /// <returns>A props bag keyed by <c>onEvent</c>; empty when <paramref name="value"/> is not a bag.</returns>
    public static VirtualNodeProperties _toHandlers(object? value)
    {
        var result = new VirtualNodeProperties();
        if (value is VirtualNodeProperties bag)
        {
            foreach (var (name, handler) in bag)
            {
                result.Set(_toHandlerKey(name), handler);
            }
        }
        return result;
    }

    /// <summary>Camel-cases a hyphenated name (upstream: <c>camelize</c>).</summary>
    /// <param name="value">The name.</param>
    public static string _camelize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ComponentInstance.Camelize(value);
    }

    /// <summary>Capitalizes the first character (upstream: <c>capitalize</c>).</summary>
    /// <param name="value">The name.</param>
    public static string _capitalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }

    /// <summary>Builds an <c>onXxx</c> handler key from an event name (upstream: <c>toHandlerKey</c>).</summary>
    /// <param name="value">The event name.</param>
    public static string _toHandlerKey(object? value)
    {
        var name = value as string ?? DisplayStringFormatter.ToDisplayString(value);
        return name.Length == 0 ? string.Empty : "on" + _capitalize(ComponentInstance.Camelize(name));
    }

    // ==== Reactivity bridge (upstream unref / isRef) ============================================

    /// <summary>
    /// Unwraps a ref, or returns the argument itself when it is not a ref — the by-name bridge to
    /// <c>Assimalign.Viu.Reactivity</c> (upstream: <c>unref</c>). Reading a ref's value tracks it.
    /// </summary>
    /// <param name="value">The value that may be a ref.</param>
    public static object? _unref(object? value) => value is IReference reference ? reference.Value : value;

    /// <summary>Whether a value is a ref (upstream: <c>isRef</c>).</summary>
    /// <param name="value">The value to test.</param>
    public static bool _isRef(object? value) => Reactive.IsRef(value);

    // ==== Viu-defined helpers (no upstream counterpart; see docs/DESIGN.md divergence table) ===

    /// <summary>
    /// Builds a props/slots object from name/value tuples (Viu-defined). JavaScript object literals
    /// (<c>{ id: x, onClick: h }</c>) have no C# spelling, so the emitter writes
    /// <c>_createProps(("id", x), ("onClick", _withHandler(h)))</c>; the empty <c>_createProps()</c> is the
    /// <c>{}</c> placeholder (e.g. <c>renderSlot</c>'s empty props).
    /// </summary>
    /// <param name="entries">The property entries.</param>
    public static VirtualNodeProperties _createProps(params (string Name, object? Value)[] entries)
        => VirtualNodeFactory.Properties(entries);

    /// <summary>
    /// Target-types an event-handler value expression (Viu-defined). A C# lambda or method group has no
    /// natural type in the object-typed prop position, so the emitter wraps it —
    /// <c>_withHandler(__event =&gt; (_ctx.count++))</c>, <c>_withHandler(_ctx.save)</c> — and this overload
    /// supplies the delegate target type. The handler is returned unchanged (as the stored prop value);
    /// <c>_withModifiers</c>/<c>_withKeys</c> guard wrappers are <b>not</b> re-wrapped (their own signatures
    /// type the inner lambda).
    /// </summary>
    /// <param name="handler">The value-producing inline handler.</param>
    public static object? _withHandler(Func<object?, object?> handler) => handler;

    /// <summary>Target-types a void inline or method-group handler taking the event argument (Viu-defined).</summary>
    /// <param name="handler">The handler.</param>
    public static object? _withHandler(Action<object?> handler) => handler;

    /// <summary>Target-types a parameterless void method-group handler (Viu-defined).</summary>
    /// <param name="handler">The handler.</param>
    public static object? _withHandler(Action handler) => handler;

    /// <summary>Target-types a parameterless value method-group handler (Viu-defined).</summary>
    /// <param name="handler">The handler.</param>
    public static object? _withHandler(Func<object?> handler) => handler;

    /// <summary>Target-types any other delegate-shaped handler (Viu-defined catch-all for method groups).</summary>
    /// <param name="handler">The handler.</param>
    public static object? _withHandler(Delegate handler) => handler;

    /// <summary>
    /// Completes a <c>v-once</c> cache write (Viu-defined). Upstream's comma sequence
    /// <c>(setBlockTracking(-1, true), (_cache[n] = v).cacheIndex = n, setBlockTracking(1), _cache[n])</c>
    /// collapses into <c>_cache[n] ??= _setCache(n, _setBlockTracking(-1, true), v)</c>: argument evaluation
    /// pauses tracking before <c>v</c> is created, then this call resumes tracking (inverting
    /// <paramref name="tracking"/>) and returns <paramref name="value"/>. The cache index stamp is a
    /// documented no-op — <see cref="VirtualNode"/> has no <c>cacheIndex</c>; <c>v-once</c> reuse works
    /// purely through the <c>_cache</c> slot.
    /// </summary>
    /// <param name="index">The cache slot index (accepted for parity; not stamped onto the vnode).</param>
    /// <param name="tracking">The token from the paired <see cref="_setBlockTracking"/> call.</param>
    /// <param name="value">The freshly created (tracking-suspended) value to cache.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static object? _setCache(int index, BlockToken tracking, object? value)
    {
        VirtualNodeFactory.SetBlockTracking(-tracking.TrackingDelta);
        _ = index;
        return value;
    }

    /// <summary>
    /// Clones a cached array so a block's children are a fresh array on reuse (Viu-defined). Upstream's
    /// <c>[...(cacheExpr)]</c> spread has no C# counterpart, so the emitter writes
    /// <c>_spreadCache(cacheExpr)</c>.
    /// </summary>
    /// <param name="value">The cached array (or any value; non-arrays pass through).</param>
    public static object? _spreadCache(object? value) => value switch
    {
        VirtualNode?[] virtualNodes => (VirtualNode?[])virtualNodes.Clone(),
        object?[] array => (object?[])array.Clone(),
        _ => value,
    };

    // ==== Built-in tags as values (upstream @vue/runtime-core exports) ===========================

    /// <summary>The <c>Fragment</c> block type (upstream: <c>Fragment</c>).</summary>
    public static readonly object _Fragment = new BuiltInVirtualNodeType("Fragment", isFragment: true);

    /// <summary>
    /// The <c>Teleport</c> built-in (upstream: <c>Teleport</c>), realized by the renderer's Teleport
    /// patch/move/unmount paths ([V01.01.03.17]). The compiled render passes it as a vnode <c>tag</c>;
    /// <see cref="CreateBaseVNode"/> routes it to <see cref="VirtualNodeFactory.Teleport"/>.
    /// </summary>
    public static readonly object _Teleport = new BuiltInVirtualNodeType("Teleport", isFragment: false, isTeleport: true);

    /// <summary>The <c>Suspense</c> built-in marker (upstream: <c>Suspense</c>); renderer support is separate work.</summary>
    public static readonly object _Suspense = new BuiltInVirtualNodeType("Suspense", isFragment: false);

    /// <summary>
    /// The <c>KeepAlive</c> built-in (upstream: <c>KeepAlive</c>), resolved to the real caching component
    /// <see cref="RuntimeCore.KeepAlive"/> ([V01.01.03.18]). The compiled render passes it as a vnode
    /// <c>tag</c>; <see cref="CreateBaseVNode"/>'s component arm mounts it, and the renderer's
    /// activate/deactivate paths cache its child's subtree instead of unmounting it.
    /// </summary>
    public static readonly object _KeepAlive = KeepAlive.Instance;

    /// <summary>
    /// The <c>BaseTransition</c> built-in (upstream: <c>BaseTransition</c>), resolved to the real
    /// transition state-machine component <see cref="RuntimeCore.BaseTransition"/> ([V01.01.04.07]).
    /// The compiled render passes it as a vnode <c>tag</c>; the vnode factory's component-definition arm
    /// mounts it (the DOM <c>&lt;Transition&gt;</c>/<c>&lt;TransitionGroup&gt;</c> wrap it with resolved
    /// class hooks).
    /// </summary>
    public static readonly object _BaseTransition = BaseTransition.Instance;

    // ==== Render-root normalization (upstream normalizeVNode over the render return) =============

    /// <summary>
    /// Normalizes a compiled render function's <c>object?</c> return into a vnode — the C# analogue of
    /// upstream <c>normalizeVNode</c> applied to the render result (a single text/interpolation root returns
    /// a raw string, so the generated <c>Render</c> return type is <c>object?</c>). Null and booleans become
    /// a comment placeholder, a string becomes text, an array becomes a fragment, and a vnode passes through
    /// (the renderer clones it if already mounted).
    /// </summary>
    /// <param name="renderResult">The value returned by a compiled <c>Render</c> function.</param>
    /// <returns>The normalized root vnode.</returns>
    public static VirtualNode NormalizeRoot(object? renderResult) => renderResult switch
    {
        null => VirtualNodeFactory.Comment(),
        VirtualNode vnode => vnode,
        string text => VirtualNodeFactory.Text(text),
        bool => VirtualNodeFactory.Comment(),
        VirtualNode?[] virtualNodes => VirtualNodeFactory.Fragment(virtualNodes),
        object?[] array => VirtualNodeFactory.Fragment(CoerceChildrenArray(array)),
        _ => VirtualNodeFactory.Text(DisplayStringFormatter.ToDisplayString(renderResult)),
    };

    // ==== Internal dispatch / coercion ==========================================================

    private static VirtualNode CreateBaseVNode(
        object? tag,
        object? properties,
        object? children,
        int patchFlag,
        string[]? dynamicProperties,
        bool asBlock)
    {
        var bag = properties as VirtualNodeProperties;
        var flag = (PatchFlags)patchFlag;
        switch (tag)
        {
            case string elementTag:
                return CreateElement(elementTag, bag, children, flag, dynamicProperties, asBlock);
            case BuiltInVirtualNodeType { IsFragment: true }:
                var fragmentChildren = CoerceChildren(children);
                var key = bag is not null && bag.TryGetValue("key", out var keyValue) ? keyValue : null;
                return asBlock
                    ? VirtualNodeFactory.FragmentBlock(fragmentChildren, key, flag)
                    : VirtualNodeFactory.Fragment(fragmentChildren, key, flag);
            case BuiltInVirtualNodeType { IsTeleport: true }:
                // Teleport children are always an array (never built as slots — TransformElement excludes
                // Teleport from the single-child collapse and the slot build), so coerce to the vnode array.
                var teleportChildren = CoerceChildren(children);
                return asBlock
                    ? VirtualNodeFactory.TeleportBlock(bag, teleportChildren, flag, dynamicProperties)
                    : VirtualNodeFactory.Teleport(bag, teleportChildren, flag, dynamicProperties);
            case IComponentDefinition definition:
                var slots = CoerceSlots(children);
                return asBlock
                    ? VirtualNodeFactory.ComponentBlock(definition, bag, slots, flag, dynamicProperties)
                    : VirtualNodeFactory.Component(definition, bag, slots, flag, dynamicProperties);
            case BuiltInVirtualNodeType builtIn:
                throw new NotSupportedException(
                    $"The built-in component <{builtIn.Name}> is not yet supported by the runtime renderer.");
            case null:
                // resolveDynamicComponent(null) or an absent :is renders a comment placeholder (upstream).
                return VirtualNodeFactory.Comment();
            default:
                throw new NotSupportedException($"Unsupported vnode tag of type '{tag.GetType().Name}'.");
        }
    }

    private static VirtualNode CreateElement(
        string tag,
        VirtualNodeProperties? properties,
        object? children,
        PatchFlags patchFlag,
        string[]? dynamicProperties,
        bool asBlock)
    {
        if (children is string text)
        {
            return asBlock
                ? VirtualNodeFactory.ElementBlock(tag, properties, text, patchFlag, dynamicProperties)
                : VirtualNodeFactory.Element(tag, properties, text, patchFlag, dynamicProperties);
        }
        var childArray = CoerceChildren(children);
        return asBlock
            ? VirtualNodeFactory.ElementBlock(tag, properties, childArray, patchFlag, dynamicProperties)
            : VirtualNodeFactory.Element(tag, properties, childArray, patchFlag, dynamicProperties);
    }

    // Coerces the untyped `children` argument the emitter writes (a single vnode, a string, a
    // VirtualNode?[] from _renderList, or an object?[] literal) into the factory's VirtualNode?[] shape,
    // per upstream normalizeVNode of each child. VirtualNode?[] is checked before object?[] because array
    // covariance makes a VirtualNode?[] also match object?[].
    private static VirtualNode?[]? CoerceChildren(object? children) => children switch
    {
        null => null,
        VirtualNode?[] virtualNodes => virtualNodes,
        object?[] array => CoerceChildrenArray(array),
        VirtualNode single => [single],
        string text => [VirtualNodeFactory.Text(text)],
        _ => [CoerceChild(children)],
    };

    private static VirtualNode?[] CoerceChildrenArray(object?[] array)
    {
        var result = new VirtualNode?[array.Length];
        for (var index = 0; index < array.Length; index++)
        {
            result[index] = CoerceChild(array[index]);
        }
        return result;
    }

    private static VirtualNode? CoerceChild(object? child) => child switch
    {
        null => null,
        VirtualNode vnode => vnode,
        string text => VirtualNodeFactory.Text(text),
        VirtualNode?[] virtualNodes => VirtualNodeFactory.Fragment(virtualNodes),
        object?[] array => VirtualNodeFactory.Fragment(CoerceChildrenArray(array)),
        _ => VirtualNodeFactory.Text(DisplayStringFormatter.ToDisplayString(child)),
    };

    private static VirtualNode?[]? CoerceVirtualNodeArray(object?[]? array)
    {
        if (array is null)
        {
            return null;
        }
        return CoerceChildrenArray(array);
    }

    private static ComponentSlots? CoerceSlots(object? children)
    {
        switch (children)
        {
            case null:
                return null;
            case ComponentSlots slots:
                return slots;
            case VirtualNodeProperties bag:
                return BuildSlots(bag);
            case Slot slot:
                return DefaultSlot(slot);
            case VirtualNode?[] virtualNodes:
                return DefaultSlot(_ => virtualNodes);
            case object?[] array:
                var coerced = CoerceChildrenArray(array);
                return DefaultSlot(_ => coerced);
            case VirtualNode single:
                return DefaultSlot(_ => [single]);
            default:
                return null;
        }
    }

    private static ComponentSlots BuildSlots(VirtualNodeProperties bag)
    {
        var slots = new ComponentSlots();
        foreach (var (name, value) in bag)
        {
            if (string.Equals(name, "_", StringComparison.Ordinal))
            {
                // The hidden slot-stability marker (upstream slots._): the emitter writes ("_", <flag>).
                if (value is int flag)
                {
                    slots.Flag = (SlotFlags)flag;
                }
                continue;
            }
            switch (value)
            {
                case Slot slot:
                    slots[name] = slot;
                    break;
                case Func<object?, object?[]?> scoped:
                    slots[name] = properties => CoerceVirtualNodeArray(scoped(properties));
                    break;
                case Func<object?[]?> plain:
                    slots[name] = _ => CoerceVirtualNodeArray(plain());
                    break;
            }
        }
        return slots;
    }

    private static ComponentSlots DefaultSlot(Slot slot)
    {
        var slots = new ComponentSlots();
        slots["default"] = slot;
        return slots;
    }

    private static string CoerceText(object? text) => text switch
    {
        null => string.Empty,
        string value => value,
        _ => DisplayStringFormatter.ToDisplayString(text),
    };
}
