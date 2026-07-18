namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// A single named slot — the C# port of upstream's <c>Slot</c> function type
/// (<c>packages/runtime-core/src/componentSlots.ts</c>,
/// https://vuejs.org/guide/components/slots.html). A slot is a plain delegate that produces vnodes
/// on demand, capturing its defining render context; the child invokes it through
/// <see cref="VirtualNodeFactory.RenderSlot"/> when it reaches the corresponding outlet. The
/// <paramref name="properties"/> argument carries the child-supplied scope for a scoped slot (the
/// object passed to <c>&lt;slot :x="…"/&gt;</c>); a non-scoped slot simply ignores it. Returning
/// null or an empty array renders nothing. No reflection or boxing beyond the caller's own
/// arguments — the delegate is the entire slot, matching the AOT/trimming contract.
/// </summary>
/// <param name="properties">The scoped-slot props supplied by the child, or null.</param>
/// <returns>The slot's vnodes, or null for empty content.</returns>
public delegate VirtualNode?[]? Slot(object? properties);
