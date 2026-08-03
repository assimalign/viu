using System;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Optimization hints the Viu template compiler stamps onto a render node so the runtime patch
/// algorithm updates only the parts of the node that are actually able to change. The bit layout —
/// including the whole-value negative sentinels <see cref="Cached"/> and <see cref="Bail"/> — is a
/// frozen contract between compiled output and the runtime: changing a value silently breaks
/// components compiled by an earlier Viu, so values are additive only. Specified by
/// <c>[RND-FLAGS-1]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Positive flags are single bits and may be combined with the bitwise OR operator, e.g.
/// <c>PatchFlags.Text | PatchFlags.Class</c>. <see cref="Cached"/> (<c>-1</c>) and
/// <see cref="Bail"/> (<c>-2</c>) are whole-value sentinels, never bit combinations: because
/// every negative <see cref="int"/> has most bits set, a naive bitwise test against a negative
/// value would spuriously succeed. Always gate positive-bit checks on <c>flags &gt; 0</c> —
/// the predicates in <c>PatchFlagsExtensions</c> do this for you. Specified by
/// <c>[RND-FLAGS-3]</c>.
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
    /// transform (<c>TransformText</c>) when an element's children collapse to one dynamic
    /// text expression.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Indicates an element with a dynamic <c>class</c> binding. Emitted by the element
    /// transform's property analysis (<c>TransformElement</c>/<c>PropertyBuilder</c>) when
    /// <c>:class</c> is bound to a dynamic expression.
    /// </summary>
    Class = 1 << 1,

    /// <summary>
    /// Indicates an element with a dynamic <c>style</c> binding. Emitted by the element
    /// transform's property analysis (<c>TransformElement</c>/<c>PropertyBuilder</c>) when
    /// <c>:style</c> is bound to a dynamic expression (static style strings are compiled into
    /// cached objects instead).
    /// </summary>
    Style = 1 << 2,

    /// <summary>
    /// Indicates an element with dynamic properties other than <c>class</c> and <c>style</c>. The
    /// node also carries the list of dynamic property names so the runtime can diff only those
    /// names. Emitted by <c>TransformElement</c>/<c>PropertyBuilder</c>; mutually exclusive with
    /// <see cref="FullProps"/>.
    /// </summary>
    Props = 1 << 3,

    /// <summary>
    /// Indicates an element whose properties require a full diff because the property names
    /// themselves are dynamic — a <c>v-bind</c> or <c>v-on</c> with a dynamic argument, or an
    /// object <c>v-bind</c> spread. Emitted by <c>TransformElement</c>/<c>PropertyBuilder</c>;
    /// when set it replaces <see cref="Class"/>, <see cref="Style"/>, and <see cref="Props"/>.
    /// </summary>
    FullProps = 1 << 4,

    /// <summary>
    /// Indicates an element or component that requires extra work during hydration beyond property
    /// patching — attaching event listeners or applying <c>v-show</c>. Emitted by
    /// <c>TransformElement</c>/<c>PropertyBuilder</c> (event handlers) and the <c>v-show</c>
    /// directive transform. Value: <c>32</c>.
    /// </summary>
    NeedHydration = 1 << 5,

    /// <summary>
    /// Indicates a fragment whose children order never changes, so children can be patched
    /// pairwise without reconciliation. Emitted by the compiler for multi-root templates and
    /// other structurally stable fragments.
    /// </summary>
    StableFragment = 1 << 6,

    /// <summary>
    /// Indicates a fragment with keyed (or partially keyed) children requiring keyed
    /// reconciliation. Emitted by the <c>v-for</c> transform (<c>VForTransform</c>) when the
    /// iterated nodes carry <c>key</c> bindings.
    /// </summary>
    KeyedFragment = 1 << 7,

    /// <summary>
    /// Indicates a fragment with entirely unkeyed children, patched by index. Emitted by the
    /// <c>v-for</c> transform (<c>VForTransform</c>) when no <c>key</c> bindings are present.
    /// </summary>
    UnkeyedFragment = 1 << 8,

    /// <summary>
    /// Indicates an element that needs only non-property patching — it carries a template
    /// reference, runtime directives, or node lifecycle hooks that must be processed even though
    /// no property changes. Emitted by <c>TransformElement</c>/<c>PropertyBuilder</c>.
    /// </summary>
    NeedPatch = 1 << 9,

    /// <summary>
    /// Indicates a component with dynamic slots — slot content using <c>v-if</c>/<c>v-for</c>
    /// or dynamic slot names — so the child must always be force-updated when slots may have
    /// changed. Emitted by the slot transforms (<c>TransformSlotOutlet</c>/<c>VSlotTransform</c>).
    /// </summary>
    DynamicSlots = 1 << 10,

    /// <summary>
    /// Development-only flag indicating a fragment that was created solely because the user
    /// placed comments at the root of the template. Emitted only by development-mode compilation
    /// so hot-reload and devtools can treat the fragment transparently.
    /// </summary>
    DevRootFragment = 1 << 11,

    /// <summary>
    /// Whole-value sentinel (<c>-1</c>) indicating a cached static node: the diff skips the entire
    /// subtree because it can never change. Emitted by the static-caching pass
    /// (<c>StaticCache</c>). Never combined with other flags — test with equality, not bitwise AND.
    /// </summary>
    Cached = -1,

    /// <summary>
    /// Whole-value sentinel (<c>-2</c>) indicating the diff algorithm must bail out of optimized
    /// mode and perform a full diff — produced at runtime (not by a compiler transform) for
    /// nodes the compiler did not generate, e.g. cloned or hand-authored render output. Never
    /// combined with other flags — test with equality, not bitwise AND.
    /// </summary>
    Bail = -2,
}
