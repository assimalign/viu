using System.Runtime.CompilerServices;

namespace Assimalign.Vue.Shared;

/// <summary>
/// Allocation-free bitwise predicates over <see cref="PatchFlags"/>. All positive-flag checks
/// are gated on both operands being positive, so the negative sentinels
/// (<see cref="PatchFlags.Cached"/> and <see cref="PatchFlags.Bail"/>, whose two's-complement
/// representations have most bits set) never spuriously satisfy them from either side — matching
/// upstream's <c>patchFlag &gt; 0</c> fast-path guard. Sentinels are tested with
/// <see cref="IsCached"/>/<see cref="IsBail"/> instead. Every predicate is a plain inlineable
/// bitwise/equality check; <see cref="Enum.HasFlag"/> is never used.
/// </summary>
public static class PatchFlagsExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="flags"/> is a positive flag
    /// combination containing any bit of <paramref name="flag"/>. Returns
    /// <see langword="false"/> when either operand is non-positive: the negative sentinels
    /// <see cref="PatchFlags.Cached"/> and <see cref="PatchFlags.Bail"/> satisfy no bitwise
    /// check, whether they appear as the receiver or the argument (use
    /// <see cref="IsCached"/>/<see cref="IsBail"/> to test for them).
    /// </summary>
    /// <param name="flags">The vnode's patch flags.</param>
    /// <param name="flag">The positive flag (or flag combination) to test for.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has(this PatchFlags flags, PatchFlags flag) => flags > 0 && flag > 0 && (flags & flag) != 0;

    /// <summary>Returns <see langword="true"/> when the vnode has dynamic text content (<see cref="PatchFlags.Text"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasText(this PatchFlags flags) => flags.Has(PatchFlags.Text);

    /// <summary>Returns <see langword="true"/> when the vnode has a dynamic <c>class</c> binding (<see cref="PatchFlags.Class"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDynamicClass(this PatchFlags flags) => flags.Has(PatchFlags.Class);

    /// <summary>Returns <see langword="true"/> when the vnode has a dynamic <c>style</c> binding (<see cref="PatchFlags.Style"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDynamicStyle(this PatchFlags flags) => flags.Has(PatchFlags.Style);

    /// <summary>Returns <see langword="true"/> when the vnode has dynamic non-class/style props (<see cref="PatchFlags.Props"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDynamicProps(this PatchFlags flags) => flags.Has(PatchFlags.Props);

    /// <summary>Returns <see langword="true"/> when the vnode requires a full props diff (<see cref="PatchFlags.FullProps"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasFullProps(this PatchFlags flags) => flags.Has(PatchFlags.FullProps);

    /// <summary>Returns <see langword="true"/> when the vnode needs listener/<c>v-show</c> work during hydration (<see cref="PatchFlags.NeedHydration"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsHydration(this PatchFlags flags) => flags.Has(PatchFlags.NeedHydration);

    /// <summary>Returns <see langword="true"/> when the vnode is a fragment with stable children order (<see cref="PatchFlags.StableFragment"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStableFragment(this PatchFlags flags) => flags.Has(PatchFlags.StableFragment);

    /// <summary>Returns <see langword="true"/> when the vnode is a fragment with keyed children (<see cref="PatchFlags.KeyedFragment"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsKeyedFragment(this PatchFlags flags) => flags.Has(PatchFlags.KeyedFragment);

    /// <summary>Returns <see langword="true"/> when the vnode is a fragment with unkeyed children (<see cref="PatchFlags.UnkeyedFragment"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnkeyedFragment(this PatchFlags flags) => flags.Has(PatchFlags.UnkeyedFragment);

    /// <summary>Returns <see langword="true"/> when the vnode needs non-props patching such as refs or directives (<see cref="PatchFlags.NeedPatch"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsPatch(this PatchFlags flags) => flags.Has(PatchFlags.NeedPatch);

    /// <summary>Returns <see langword="true"/> when the vnode is a component with dynamic slots (<see cref="PatchFlags.DynamicSlots"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDynamicSlots(this PatchFlags flags) => flags.Has(PatchFlags.DynamicSlots);

    /// <summary>Returns <see langword="true"/> when the vnode is a development-only root fragment created for root-level comments (<see cref="PatchFlags.DevRootFragment"/>).</summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDevRootFragment(this PatchFlags flags) => flags.Has(PatchFlags.DevRootFragment);

    /// <summary>
    /// Returns <see langword="true"/> when the vnode is a cached static vnode
    /// (<see cref="PatchFlags.Cached"/>, the <c>-1</c> sentinel). Compared with equality because
    /// the sentinel is a whole value, never a bit combination.
    /// </summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCached(this PatchFlags flags) => flags == PatchFlags.Cached;

    /// <summary>
    /// Returns <see langword="true"/> when the vnode demands a full-diff bail-out
    /// (<see cref="PatchFlags.Bail"/>, the <c>-2</c> sentinel). Compared with equality because
    /// the sentinel is a whole value, never a bit combination.
    /// </summary>
    /// <param name="flags">The vnode's patch flags.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBail(this PatchFlags flags) => flags == PatchFlags.Bail;
}
