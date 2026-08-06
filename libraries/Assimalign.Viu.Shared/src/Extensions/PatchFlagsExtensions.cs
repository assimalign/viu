using System.Runtime.CompilerServices;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Allocation-free bitwise predicates over <see cref="PatchFlags"/>. All positive-flag checks
/// are gated on both operands being positive, so the negative sentinels
/// (<see cref="PatchFlags.Cached"/> and <see cref="PatchFlags.Bail"/>, whose two's-complement
/// representations have most bits set) never spuriously satisfy them from either side. That
/// <c>flags &gt; 0</c> guard is the required form of every positive-bit test. Sentinels are tested with
/// <see cref="IsCached"/>/<see cref="IsBail"/> instead. Every predicate is a plain inlineable
/// bitwise/equality check; <see cref="System.Enum.HasFlag(System.Enum)"/> is never used.
/// </summary>
public static class PatchFlagsExtensions
{
    /// <param name="flags">The vnode's patch flags.</param>
    extension(PatchFlags flags)
    {
        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="flags"/> is a positive flag
        /// combination containing any bit of <paramref name="flag"/>. Returns
        /// <see langword="false"/> when either operand is non-positive: the negative sentinels
        /// <see cref="PatchFlags.Cached"/> and <see cref="PatchFlags.Bail"/> satisfy no bitwise
        /// check, whether they appear as the receiver or the argument (use
        /// <see cref="IsCached"/>/<see cref="IsBail"/> to test for them).
        /// </summary>
        /// <param name="flag">The positive flag (or flag combination) to test for.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(PatchFlags flag) => flags > 0 && flag > 0 && (flags & flag) != 0;

        /// <summary>Returns <see langword="true"/> when the vnode has dynamic text content (<see cref="PatchFlags.Text"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasText() => flags.Has(PatchFlags.Text);

        /// <summary>Returns <see langword="true"/> when the vnode has a dynamic <c>class</c> binding (<see cref="PatchFlags.Class"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasDynamicClass() => flags.Has(PatchFlags.Class);

        /// <summary>Returns <see langword="true"/> when the vnode has a dynamic <c>style</c> binding (<see cref="PatchFlags.Style"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasDynamicStyle() => flags.Has(PatchFlags.Style);

        /// <summary>Returns <see langword="true"/> when the vnode has dynamic non-class/style props (<see cref="PatchFlags.Props"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasDynamicProps() => flags.Has(PatchFlags.Props);

        /// <summary>Returns <see langword="true"/> when the vnode requires a full props diff (<see cref="PatchFlags.FullProps"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFullProps() => flags.Has(PatchFlags.FullProps);

        /// <summary>Returns <see langword="true"/> when the vnode needs listener/<c>v-show</c> work during hydration (<see cref="PatchFlags.NeedHydration"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NeedsHydration() => flags.Has(PatchFlags.NeedHydration);

        /// <summary>Returns <see langword="true"/> when the vnode is a fragment with stable children order (<see cref="PatchFlags.StableFragment"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStableFragment() => flags.Has(PatchFlags.StableFragment);

        /// <summary>Returns <see langword="true"/> when the vnode is a fragment with keyed children (<see cref="PatchFlags.KeyedFragment"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyedFragment() => flags.Has(PatchFlags.KeyedFragment);

        /// <summary>Returns <see langword="true"/> when the vnode is a fragment with unkeyed children (<see cref="PatchFlags.UnkeyedFragment"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUnkeyedFragment() => flags.Has(PatchFlags.UnkeyedFragment);

        /// <summary>Returns <see langword="true"/> when the vnode needs non-props patching such as refs or directives (<see cref="PatchFlags.NeedPatch"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NeedsPatch() => flags.Has(PatchFlags.NeedPatch);

        /// <summary>Returns <see langword="true"/> when the vnode is a component with dynamic slots (<see cref="PatchFlags.DynamicSlots"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasDynamicSlots() => flags.Has(PatchFlags.DynamicSlots);

        /// <summary>Returns <see langword="true"/> when the vnode is a development-only root fragment created for root-level comments (<see cref="PatchFlags.DevRootFragment"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDevRootFragment() => flags.Has(PatchFlags.DevRootFragment);

        /// <summary>
        /// Returns <see langword="true"/> when the vnode is a cached static vnode
        /// (<see cref="PatchFlags.Cached"/>, the <c>-1</c> sentinel). Compared with equality because
        /// the sentinel is a whole value, never a bit combination.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCached() => flags == PatchFlags.Cached;

        /// <summary>
        /// Returns <see langword="true"/> when the vnode demands a full-diff bail-out
        /// (<see cref="PatchFlags.Bail"/>, the <c>-2</c> sentinel). Compared with equality because
        /// the sentinel is a whole value, never a bit combination.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBail() => flags == PatchFlags.Bail;
    }
}
