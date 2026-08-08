using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Optimization hints the Viu template compiler stamps onto a virtual node so runtime patching
/// updates only the parts able to change. The bit layout, including the whole-value negative
/// sentinels <see cref="Cached"/> and <see cref="Bail"/>, is a frozen contract between compiled
/// output and the runtime. Values are additive only. Specified by <c>[RND-FLAGS-1]</c> and
/// <c>[RND-FLAGS-2]</c>.
/// </summary>
/// <remarks>
/// Positive members are single bits and may be combined with bitwise OR. The negative sentinels
/// are whole values, never bit combinations: because every negative <see cref="int"/> has most
/// bits set, positive-bit checks must first require <c>flags &gt; 0</c>. Specified by
/// <c>[RND-FLAGS-3]</c>. This file's path is also a linked-source compiler contract specified by
/// <c>[RND-FLAGS-6]</c>.
/// </remarks>
[Flags]
public enum PatchFlags
{
    // Deviates from the repository whole-word naming rule per design decision: Props,
    // FullProps, and DevRootFragment are frozen compiler-runtime identifiers [RND-FLAGS-1].

    /// <summary>No optimization hints; the node requires the normal full patch walk.</summary>
    None = 0,

    /// <summary>
    /// An element with dynamic <c>textContent</c>, allowing the runtime to patch only its single
    /// text payload.
    /// </summary>
    Text = 1,

    /// <summary>An element with a dynamic <c>class</c> binding.</summary>
    Class = 1 << 1,

    /// <summary>An element with a dynamic <c>style</c> binding.</summary>
    Style = 1 << 2,

    /// <summary>
    /// An element with named dynamic properties; its plan also identifies the binding indices
    /// that require comparison. Mutually exclusive with <see cref="FullProps"/>.
    /// </summary>
    Props = 1 << 3,

    /// <summary>
    /// An element whose property names are dynamic, requiring a full property diff and replacing
    /// the <see cref="Class"/>, <see cref="Style"/>, and <see cref="Props"/> shortcuts.
    /// </summary>
    FullProps = 1 << 4,

    /// <summary>
    /// An element or component requiring hydration work beyond property patching, such as
    /// attaching event listeners or applying a visibility directive.
    /// </summary>
    NeedHydration = 1 << 5,

    /// <summary>
    /// A fragment whose child order is stable, allowing children to patch pairwise without
    /// reconciliation.
    /// </summary>
    StableFragment = 1 << 6,

    /// <summary>A fragment with keyed or partially keyed children requiring keyed reconciliation.</summary>
    KeyedFragment = 1 << 7,

    /// <summary>A fragment with entirely unkeyed children patched by index.</summary>
    UnkeyedFragment = 1 << 8,

    /// <summary>
    /// An element requiring non-property work for a mount reference, runtime directive, or node
    /// lifecycle hook even though no property is dynamic.
    /// </summary>
    NeedPatch = 1 << 9,

    /// <summary>
    /// A component whose slot structure can change, requiring a child update whenever the parent
    /// renders.
    /// </summary>
    DynamicSlots = 1 << 10,

    /// <summary>
    /// Development-only marker for a root fragment created solely by root-level template comments.
    /// </summary>
    DevRootFragment = 1 << 11,

    /// <summary>
    /// Whole-value sentinel (<c>-1</c>) for a cached static subtree that diffing skips entirely.
    /// Never combine this value with another member; test it by equality.
    /// </summary>
    Cached = -1,

    /// <summary>
    /// Whole-value sentinel (<c>-2</c>) that abandons optimized mode and requires a full diff.
    /// Never combine this value with another member; test it by equality.
    /// </summary>
    Bail = -2,
}
