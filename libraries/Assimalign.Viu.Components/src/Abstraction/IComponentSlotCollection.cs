using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes named component slots together with the compiler-produced slot stability marker.
/// </summary>
/// <remarks>
/// <see cref="Flags"/> is compiler-produced and tells the renderer how far a parent re-render must
/// propagate: stable slots let the child skip a forced update, dynamic slots do not. A
/// hand-authored collection that cannot prove stability must report <see cref="SlotFlags.Dynamic"/>
/// — an over-optimistic flag manifests as a child that silently stops updating. Specified by
/// <c>[CMP-18]</c> and <c>[CMP-19]</c>.
/// </remarks>
public interface IComponentSlotCollection :
    IReadOnlyDictionary<string, ComponentSlot>
{
    /// <summary>Gets the structural stability classification for the slots.</summary>
    SlotFlags Flags { get; }
}
