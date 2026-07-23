using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Components;

internal sealed class ComponentSlotCollection :
    IComponentSlotCollection
{
    private readonly IReadOnlyDictionary<string, ComponentSlot> _slots;

    internal ComponentSlotCollection(
        IReadOnlyDictionary<string, ComponentSlot> slots,
        SlotFlags flags)
    {
        ArgumentNullException.ThrowIfNull(slots);
        _slots = new ReadOnlyDictionary<string, ComponentSlot>(
            new Dictionary<string, ComponentSlot>(slots, StringComparer.Ordinal));
        Flags = flags;
    }

    public SlotFlags Flags { get; }

    public int Count => _slots.Count;

    public IEnumerable<string> Keys => _slots.Keys;

    public IEnumerable<ComponentSlot> Values => _slots.Values;

    public ComponentSlot this[string key] => _slots[key];

    public bool ContainsKey(string key) => _slots.ContainsKey(key);

    public bool TryGetValue(string key, out ComponentSlot value) =>
        _slots.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, ComponentSlot>> GetEnumerator() =>
        _slots.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
