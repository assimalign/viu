namespace Assimalign.Viu.Components;

/// <summary>
/// Compiler-stamped slot-stability classification — a frozen compiler-runtime value contract,
/// not a bitmask. Specified by <c>[RND-FLAGS-5]</c>; moved verbatim from the dissolved shared
/// library.
/// </summary>
public enum SlotFlags
{
    /// <summary>Slots referencing only stable content; the child skips forced slot updates.</summary>
    Stable = 1,

    /// <summary>Conditionally or dynamically named slots; the child must force-update.</summary>
    Dynamic = 2,

    /// <summary>Slots forwarded from the parent's own slots; stability follows the parent.</summary>
    Forwarded = 3,
}
