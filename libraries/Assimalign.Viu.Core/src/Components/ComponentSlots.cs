using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu;

/// <summary>
/// A component's slots object — the C# port of upstream's <c>Slots</c>/<c>InternalSlots</c>
/// (<c>packages/runtime-core/src/componentSlots.ts</c>,
/// https://vuejs.org/guide/components/slots.html): a name to <see cref="Slot"/> map plus the
/// <see cref="Shared.SlotFlags"/> stability marker upstream stores on the hidden <c>_</c> property.
/// Passed on a component vnode via <see cref="VirtualNode.SlotChildren"/> and surfaced on the
/// mounted instance as <see cref="ComponentInstance.Slots"/> / <see cref="ComponentSetupContext.Slots"/>.
/// The <see cref="Flag"/> drives how aggressively a parent re-render forces the child to re-render
/// (see <see cref="Shared.SlotFlags"/>) — on WASM a skipped forced update is skipped patch-and-interop
/// work. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class ComponentSlots
{
    private readonly Dictionary<string, Slot> _slots;

    /// <summary>Creates an empty <see cref="Shared.SlotFlags.Stable"/> slots object.</summary>
    public ComponentSlots()
        : this(SlotFlags.Stable)
    {
    }

    /// <summary>Creates an empty slots object with the given stability <paramref name="flag"/>.</summary>
    /// <param name="flag">The slot stability classification.</param>
    public ComponentSlots(SlotFlags flag)
    {
        _slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        Flag = flag;
    }

    /// <summary>
    /// The stability classification (upstream: the slots object's <c>_</c> marker). Defaults to
    /// <see cref="Shared.SlotFlags.Stable"/> — the compiler's default for structurally stable slots,
    /// and safe because a slot's reactive reads are still tracked by the consuming child regardless
    /// of this flag; only structural changes (<c>v-if</c>/<c>v-for</c>/dynamic names) need
    /// <see cref="Shared.SlotFlags.Dynamic"/>. A <see cref="Shared.SlotFlags.Forwarded"/> value is
    /// resolved to Stable or Dynamic at vnode creation
    /// (<see cref="VirtualNodeFactory.Component(IComponentDefinition, VirtualNodeProperties?, ComponentSlots?, Shared.PatchFlags, string[]?)"/>).
    /// </summary>
    public SlotFlags Flag { get; set; }

    /// <summary>The number of named slots.</summary>
    public int Count => _slots.Count;

    /// <summary>
    /// Gets or sets the slot registered under <paramref name="name"/> (upstream: <c>slots[name]</c>).
    /// Setting null removes the slot.
    /// </summary>
    /// <param name="name">The slot name (<c>"default"</c> for the default slot).</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public Slot? this[string name]
    {
        get => _slots.TryGetValue(name, out var slot) ? slot : null;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            if (value is null)
            {
                _slots.Remove(name);
            }
            else
            {
                _slots[name] = value;
            }
        }
    }

    /// <summary>Whether a slot named <paramref name="name"/> is present.</summary>
    /// <param name="name">The slot name.</param>
    public bool Contains(string name) => _slots.ContainsKey(name);

    /// <summary>Looks up the slot named <paramref name="name"/>.</summary>
    /// <param name="name">The slot name.</param>
    /// <param name="slot">The slot when present; null otherwise.</param>
    /// <returns>Whether a slot named <paramref name="name"/> is present.</returns>
    public bool TryGetSlot(string name, [NotNullWhen(true)] out Slot? slot) => _slots.TryGetValue(name, out slot);
}
