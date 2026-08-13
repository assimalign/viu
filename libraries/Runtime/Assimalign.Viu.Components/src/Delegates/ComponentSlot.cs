using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Produces a fresh virtual subtree for one slot invocation.
/// </summary>
/// <remarks>Slot laziness and stability are specified by <c>[CMP-18]</c> and <c>[CMP-19]</c>.</remarks>
/// <param name="arguments">The immutable slot arguments.</param>
/// <returns>The slot subtree, or null when the slot intentionally produces no node.</returns>
public delegate VirtualNode? ComponentSlot(IReadOnlyDictionary<string, object?> arguments);
