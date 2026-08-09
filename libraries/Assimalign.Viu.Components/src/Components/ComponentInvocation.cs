using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Carries raw immutable inputs supplied by a parent-created component node.
/// </summary>
/// <remarks>
/// Arguments, slots, listeners, and directives are copied when the invocation is constructed and
/// are never contract-resolved in place. Hydration policy remains data-only invocation metadata.
/// Specified by <c>[CMP-2]</c>, <c>[CMP-7]</c>, <c>[CMP-18]</c>, and
/// <c>[HYD-LAZY-1]</c>.
/// </remarks>
public sealed class ComponentInvocation
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyArguments =
        CollectionSnapshot.CopyDictionary<object?>(null);
    private static readonly IReadOnlyDictionary<string, ComponentSlot> EmptySlots =
        CollectionSnapshot.CopyDictionary<ComponentSlot>(null);

    /// <summary>Gets an invocation with no parent-supplied inputs.</summary>
    public static ComponentInvocation Empty { get; } = new();

    /// <summary>Initializes an immutable invocation.</summary>
    /// <param name="arguments">Raw named arguments.</param>
    /// <param name="slots">Raw named slot functions.</param>
    /// <param name="listeners">Raw named event listeners.</param>
    /// <param name="directives">Directives attached to the invocation site.</param>
    /// <param name="slotStability">
    /// The compiler classification for the complete slot set. Invocations default to
    /// <see cref="Components.SlotStability.Stable"/>; callers whose slot structure can change must
    /// explicitly select <see cref="Components.SlotStability.Dynamic"/>.
    /// </param>
    /// <param name="hydrationStrategy">
    /// The optional invocation-local activation policy for adopted server markup. A null value
    /// lets an asynchronous definition supply its default; ordinary invocations hydrate eagerly.
    /// </param>
    public ComponentInvocation(
        IReadOnlyDictionary<string, object?>? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IEnumerable<DirectiveInvocation>? directives = null,
        SlotStability slotStability = SlotStability.Stable,
        HydrationStrategy? hydrationStrategy = null)
    {
        if (slotStability is not SlotStability.Stable
            and not SlotStability.Dynamic
            and not SlotStability.Forwarded)
        {
            throw new ArgumentOutOfRangeException(nameof(slotStability));
        }

        Arguments = arguments is null
            ? EmptyArguments
            : CollectionSnapshot.CopyDictionary(arguments);
        Slots = slots is null
            ? EmptySlots
            : CollectionSnapshot.CopyNonNullDictionary(slots, nameof(slots));
        Listeners = CollectionSnapshot.CopyNonNullDictionary(listeners, nameof(listeners));
        Directives = CollectionSnapshot.CopyNonNull(directives, nameof(directives));
        SlotStability = slotStability;
        HydrationStrategy = hydrationStrategy;
    }

    /// <summary>Gets raw named arguments before contract resolution.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>Gets raw named slots before fallback and stability resolution.</summary>
    public IReadOnlyDictionary<string, ComponentSlot> Slots { get; }

    /// <summary>Gets raw parent listeners before declared-event consumption.</summary>
    public IReadOnlyDictionary<string, ComponentEventListener> Listeners { get; }

    /// <summary>Gets directives attached at the invocation site.</summary>
    public IReadOnlyList<DirectiveInvocation> Directives { get; }

    /// <summary>
    /// Gets the immutable structural classification for the complete slot set. A
    /// <see cref="Components.SlotStability.Forwarded"/> value is resolved against the active parent
    /// component by Core. Specified by <c>[CMP-18]</c> and <c>[CMP-19]</c>.
    /// </summary>
    public SlotStability SlotStability { get; }

    /// <summary>
    /// Gets the invocation-local hydration strategy, or null when the definition supplies the
    /// default eager behavior. Specified by <c>[HYD-LAZY-1]</c>.
    /// </summary>
    public HydrationStrategy? HydrationStrategy { get; }
}
