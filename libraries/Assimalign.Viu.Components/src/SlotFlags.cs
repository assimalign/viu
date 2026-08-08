namespace Assimalign.Viu.Components;

/// <summary>
/// Classification assigned by template compilation to a component's slot collection, telling the
/// runtime how aggressively slot content must re-render. This is a plain enumeration, not a
/// bitmask: a slot collection has exactly one value. Numeric values are a frozen, additive-only
/// compiler-runtime contract. Specified by <c>[RND-FLAGS-1]</c>, <c>[RND-FLAGS-5]</c>, and
/// <c>[RND-FLAGS-6]</c>.
/// </summary>
public enum SlotFlags
{
    /// <summary>
    /// Slots referencing only slot parameters or context-stable state; the child need not force an
    /// update solely because its parent rendered.
    /// </summary>
    Stable = 1,

    /// <summary>
    /// Slots whose structure can change through conditions, iteration, or dynamic names. A slot
    /// collection unable to prove stability must use this value; an over-optimistic value can make
    /// a child silently stop updating.
    /// </summary>
    Dynamic = 2,

    /// <summary>
    /// Slots forwarded from the parent's collection; effective stability follows the parent and is
    /// resolved to <see cref="Stable"/> or <see cref="Dynamic"/> at runtime.
    /// </summary>
    Forwarded = 3,
}
