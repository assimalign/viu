using System;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Optimization hints the template compiler stamps onto a vnode so the runtime patch algorithm
/// can update only the parts of the vnode that are actually able to change. Mirrors the
/// <c>PatchFlags</c> enum in <c>@vue/shared</c> (<c>packages/shared/src/patchFlags.ts</c>)
/// bit-for-bit, including the negative sentinels <see cref="Cached"/> and <see cref="Bail"/>.
/// </summary>
/// <remarks>
/// <para>
/// Positive flags are single bits and may be combined with the bitwise OR operator, e.g.
/// <c>PatchFlags.Text | PatchFlags.Class</c>. <see cref="Cached"/> (<c>-1</c>) and
/// <see cref="Bail"/> (<c>-2</c>) are whole-value sentinels, never bit combinations: because
/// every negative <see cref="int"/> has most bits set, a naive bitwise test against a negative
/// value would spuriously succeed. Always gate positive-bit checks on <c>flags &gt; 0</c> —
/// the predicates in <see cref="PatchFlagsExtensions"/> do this for you.
/// </para>
/// <para>
/// This is the compiler &lt;-&gt; runtime contract that enables compiler-informed patching: every
/// patch visit skipped via a flag is DOM interop work (a JSImport marshaling round-trip on WASM)
/// avoided.
/// </para>
/// </remarks>
[Flags]
public enum PatchFlags
{
    /// <summary>
    /// Indicates an element with dynamic <c>textContent</c> (children are a single dynamic
    /// interpolation, so only the text needs to be patched). Emitted by the compiler's text
    /// transform (<c>transformText</c>) when an element's children collapse to one dynamic
    /// text expression. Upstream: <c>TEXT = 1</c>.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Indicates an element with a dynamic <c>class</c> binding. Emitted by the element
    /// transform's prop analysis (<c>transformElement</c>/<c>buildProps</c>) when
    /// <c>:class</c> is bound to a dynamic expression. Upstream: <c>CLASS = 1 &lt;&lt; 1</c>.
    /// </summary>
    Class = 1 << 1,

    /// <summary>
    /// Indicates an element with a dynamic <c>style</c> binding. Emitted by the element
    /// transform's prop analysis (<c>transformElement</c>/<c>buildProps</c>) when
    /// <c>:style</c> is bound to a dynamic expression (static style strings are compiled into
    /// cached objects instead). Upstream: <c>STYLE = 1 &lt;&lt; 2</c>.
    /// </summary>
    Style = 1 << 2,

    /// <summary>
    /// Indicates an element with dynamic props other than <c>class</c> and <c>style</c>. The
    /// vnode also carries the list of dynamic prop keys (<c>dynamicProps</c>) so the runtime can
    /// diff only those keys. Emitted by <c>transformElement</c>/<c>buildProps</c>; mutually
    /// exclusive with <see cref="FullProps"/>. Upstream: <c>PROPS = 1 &lt;&lt; 3</c>.
    /// </summary>
    Props = 1 << 3,

    /// <summary>
    /// Indicates an element whose props require a full diff because prop keys themselves are
    /// dynamic — a <c>v-bind</c> or <c>v-on</c> with a dynamic argument, or an object
    /// <c>v-bind</c> spread. Emitted by <c>transformElement</c>/<c>buildProps</c>; when set it
    /// replaces <see cref="Class"/>, <see cref="Style"/>, and <see cref="Props"/>.
    /// Upstream: <c>FULL_PROPS = 1 &lt;&lt; 4</c>.
    /// </summary>
    FullProps = 1 << 4,

    /// <summary>
    /// Indicates an element or component that requires extra work during hydration beyond prop
    /// patching — attaching event listeners or applying <c>v-show</c>. Emitted by
    /// <c>transformElement</c>/<c>buildProps</c> (event handlers) and the <c>v-show</c>
    /// directive transform. Upstream: <c>NEED_HYDRATION = 1 &lt;&lt; 5</c> (formerly
    /// <c>HYDRATE_EVENTS</c>). Value: <c>32</c>.
    /// </summary>
    NeedHydration = 1 << 5,

    /// <summary>
    /// Indicates a fragment whose children order never changes, so children can be patched
    /// pairwise without reconciliation. Emitted by the compiler for multi-root templates and
    /// other structurally stable fragments. Upstream: <c>STABLE_FRAGMENT = 1 &lt;&lt; 6</c>.
    /// </summary>
    StableFragment = 1 << 6,

    /// <summary>
    /// Indicates a fragment with keyed (or partially keyed) children requiring keyed
    /// reconciliation. Emitted by the <c>v-for</c> transform (<c>transformFor</c>) when the
    /// iterated nodes carry <c>key</c> bindings. Upstream: <c>KEYED_FRAGMENT = 1 &lt;&lt; 7</c>.
    /// </summary>
    KeyedFragment = 1 << 7,

    /// <summary>
    /// Indicates a fragment with entirely unkeyed children, patched by index. Emitted by the
    /// <c>v-for</c> transform (<c>transformFor</c>) when no <c>key</c> bindings are present.
    /// Upstream: <c>UNKEYED_FRAGMENT = 1 &lt;&lt; 8</c>.
    /// </summary>
    UnkeyedFragment = 1 << 8,

    /// <summary>
    /// Indicates an element that needs only non-props patching — it carries a <c>ref</c>,
    /// runtime directives, or vnode lifecycle hooks that must be processed even though no props
    /// change. Emitted by <c>transformElement</c>/<c>buildProps</c>.
    /// Upstream: <c>NEED_PATCH = 1 &lt;&lt; 9</c>.
    /// </summary>
    NeedPatch = 1 << 9,

    /// <summary>
    /// Indicates a component with dynamic slots — slot content using <c>v-if</c>/<c>v-for</c>
    /// or dynamic slot names — so the child must always be force-updated when slots may have
    /// changed. Emitted by the slot transform (<c>transformSlotOutlet</c>/<c>buildSlots</c> in
    /// the <c>v-slot</c> transform). Upstream: <c>DYNAMIC_SLOTS = 1 &lt;&lt; 10</c>.
    /// </summary>
    DynamicSlots = 1 << 10,

    /// <summary>
    /// Development-only flag indicating a fragment that was created solely because the user
    /// placed comments at the root of the template. Emitted only by development-mode compilation
    /// so hot-reload and devtools can treat the fragment transparently.
    /// Upstream: <c>DEV_ROOT_FRAGMENT = 1 &lt;&lt; 11</c>.
    /// </summary>
    DevRootFragment = 1 << 11,

    /// <summary>
    /// Special sentinel (<c>-1</c>, upstream <c>CACHED</c>, formerly <c>HOISTED</c>) indicating
    /// a cached static vnode: the diff skips the entire subtree because it can never change.
    /// Emitted by the static-caching pass (<c>cacheStatic</c>, formerly <c>hoistStatic</c>).
    /// Never combined with other flags — test with equality, not bitwise AND.
    /// </summary>
    Cached = -1,

    /// <summary>
    /// Special sentinel (<c>-2</c>, upstream <c>BAIL</c>) indicating the diff algorithm must
    /// bail out of optimized mode and perform a full diff — produced at runtime (not by a
    /// compiler transform) for non-compiler-generated vnodes, e.g. cloned or user-written render
    /// function output. Never combined with other flags — test with equality, not bitwise AND.
    /// </summary>
    Bail = -2,
}
