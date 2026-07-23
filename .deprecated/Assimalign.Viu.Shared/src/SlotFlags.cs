namespace Assimalign.Viu.Shared;

/// <summary>
/// Classification the compiler's <c>v-slot</c> transform (<c>buildSlots</c>) assigns to a
/// component's compiled slots object, telling the runtime how aggressively slot content must be
/// re-rendered. Mirrors the <c>SlotFlags</c> enum in <c>@vue/shared</c>
/// (<c>packages/shared/src/slotFlags.ts</c>) value-for-value. This is a plain enumeration, not a
/// bitmask — a slots object has exactly one of these values.
/// </summary>
public enum SlotFlags
{
    /// <summary>
    /// Stable slots that reference only slot props or context-stable state: the child only needs
    /// to update when the parent itself re-renders. Upstream: <c>STABLE = 1</c>.
    /// </summary>
    Stable = 1,

    /// <summary>
    /// Slots whose structure can change — they use <c>v-if</c>/<c>v-for</c> or dynamic slot
    /// names — so the child must be force-updated whenever the parent renders.
    /// Upstream: <c>DYNAMIC = 2</c>.
    /// </summary>
    Dynamic = 2,

    /// <summary>
    /// The component forwards its own slots to a child via <c>&lt;slot/&gt;</c>: whether the
    /// forwarded slots are dynamic depends on the parent's slots, so this is resolved to
    /// <see cref="Stable"/> or <see cref="Dynamic"/> at runtime.
    /// Upstream: <c>FORWARDED = 3</c>.
    /// </summary>
    Forwarded = 3,
}
