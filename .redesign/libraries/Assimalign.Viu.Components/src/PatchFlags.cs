using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Compiler-stamped optimization hints letting the runtime patch only what can change. The bit
/// layout — including the whole-value negative sentinels <see cref="Cached"/> and
/// <see cref="Bail"/> — is a frozen, additive-only contract between compiled output and the
/// runtime. Specified by <c>[RND-FLAGS-1]</c>. Moved verbatim from the dissolved shared library;
/// the compiler host's linked-source path updates in the same change (<c>[RND-FLAGS]</c> frozen
/// paths).
/// </summary>
/// <remarks>
/// Positive flags are single bits and combine with bitwise OR. The negative sentinels are whole
/// values, never combinations — gate positive-bit checks on <c>flags &gt; 0</c> per
/// <c>[RND-FLAGS-3]</c>. <see cref="None"/> names the previously unnamed zero value; adding a
/// name for zero is additive.
/// </remarks>
[Flags]
public enum PatchFlags
{
    /// <summary>No optimization hints; the node patches through the normal walk.</summary>
    None = 0,

    /// <summary>An element whose only dynamic content is one text interpolation.</summary>
    Text = 1,

    /// <summary>An element with a dynamic class binding.</summary>
    Class = 1 << 1,

    /// <summary>An element with a dynamic style binding.</summary>
    Style = 1 << 2,

    /// <summary>An element with named dynamic properties diffed by name list.</summary>
    Props = 1 << 3,

    /// <summary>An element whose property names are themselves dynamic; full property diff.</summary>
    FullProps = 1 << 4,

    /// <summary>An element or component needing extra hydration work (listeners, show directive).</summary>
    NeedHydration = 1 << 5,

    /// <summary>A fragment whose child order never changes; pairwise child patching.</summary>
    StableFragment = 1 << 6,

    /// <summary>A fragment with keyed children requiring keyed reconciliation.</summary>
    KeyedFragment = 1 << 7,

    /// <summary>A fragment with entirely unkeyed children patched by index.</summary>
    UnkeyedFragment = 1 << 8,

    /// <summary>An element needing only non-property patching (references, directives, hooks).</summary>
    NeedPatch = 1 << 9,

    /// <summary>A component with dynamic slots that must always force-update its child.</summary>
    DynamicSlots = 1 << 10,

    /// <summary>Development-only: a root fragment created solely by template root comments.</summary>
    DevRootFragment = 1 << 11,

    /// <summary>Whole-value sentinel (-1): a cached static subtree the diff skips entirely.</summary>
    Cached = -1,

    /// <summary>Whole-value sentinel (-2): bail out of optimized mode and diff fully.</summary>
    Bail = -2,
}
