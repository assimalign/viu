using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes named component slots together with the compiler-produced slot stability marker.
/// </summary>
/// <remarks>
/// Mirrors Vue 3.5's internal slots object and its hidden <c>_</c> marker:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/shared/src/slotFlags.ts.
/// </remarks>
public interface IComponentSlotCollection :
    IReadOnlyDictionary<string, ComponentSlot>
{
    /// <summary>Gets the structural stability classification for the slots.</summary>
    SlotFlags Flags { get; }
}
