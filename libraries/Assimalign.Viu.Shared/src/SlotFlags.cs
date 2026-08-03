namespace Assimalign.Viu.Shared;

/// <summary>
/// Classification the compiler's <c>v-slot</c> transform assigns to a component's compiled slot
/// collection, telling the runtime how aggressively slot content must be re-rendered. This is a
/// plain enumeration, not a bitmask — a slot collection has exactly one of these values. The
/// numeric values are a frozen contract between compiled output and the runtime and are additive
/// only. Specified by <c>[RND-FLAGS-5]</c>.
/// </summary>
public enum SlotFlags
{
    /// <summary>
    /// Stable slots that reference only slot parameters or context-stable state: the child only
    /// needs to update when the parent itself re-renders.
    /// </summary>
    Stable = 1,

    /// <summary>
    /// Slots whose structure can change — they use <c>v-if</c>/<c>v-for</c> or dynamic slot
    /// names — so the child must be force-updated whenever the parent renders. A slot collection
    /// that cannot prove stability must report this value: an over-optimistic flag manifests as a
    /// child that silently stops updating.
    /// </summary>
    Dynamic = 2,

    /// <summary>
    /// The component forwards its own slots to a child via <c>&lt;slot/&gt;</c>: whether the
    /// forwarded slots are dynamic depends on the parent's slots, so this is resolved to
    /// <see cref="Stable"/> or <see cref="Dynamic"/> at runtime.
    /// </summary>
    Forwarded = 3,
}
