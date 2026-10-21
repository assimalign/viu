using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Produces a fresh virtual subtree for one slot invocation.
/// </summary>
/// <remarks>
/// Reactive reads belong to the invoking render effect; generated content retains the declaring
/// mount's caches and collects a complete block for the outlet to track. Slot laziness, stability,
/// and nested updates are specified by <c>[CMP-18]</c>, <c>[CMP-19]</c>, and <c>[CMP-37]</c>.
/// </remarks>
/// <param name="arguments">The immutable slot arguments.</param>
/// <returns>The slot subtree, or null when the slot intentionally produces no node.</returns>
public delegate VirtualNode? ComponentSlot(IReadOnlyDictionary<string, object?> arguments);
