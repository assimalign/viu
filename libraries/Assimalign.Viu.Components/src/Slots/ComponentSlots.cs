using System;
using System.Collections;
using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Components;

/// <summary>
/// Builds a named slot collection with Vue-compatible structural stability metadata.
/// </summary>
/// <remarks>
/// This mutable builder is intended to be populated before it is passed to
/// <see cref="ComponentTree.Template(System.Type, IComponentArguments?, IReadOnlyDictionary{string, ComponentSlot}?, object?, ComponentOptimization?, IReadOnlyDictionary{string, ComponentEventListener}?, IReadOnlyList{IComponentDirectiveBinding}?, IComponentReference?)"/>.
/// Template requests take an immutable snapshot. This type is not thread-safe because Viu targets
/// the browser event loop.
/// </remarks>
public sealed class ComponentSlots :
    IComponentSlotCollection
{
    private readonly Dictionary<string, ComponentSlot> _slots =
        new(StringComparer.Ordinal);

    /// <summary>Creates a stable slot collection.</summary>
    public ComponentSlots()
        : this(SlotFlags.Stable)
    {
    }

    /// <summary>Creates a slot collection with the specified stability.</summary>
    /// <param name="flags">The structural stability classification.</param>
    public ComponentSlots(SlotFlags flags)
    {
        Flags = flags;
    }

    /// <inheritdoc/>
    public SlotFlags Flags { get; set; }

    /// <inheritdoc/>
    public int Count => _slots.Count;

    /// <inheritdoc/>
    public IEnumerable<string> Keys => _slots.Keys;

    /// <inheritdoc/>
    public IEnumerable<ComponentSlot> Values => _slots.Values;

    /// <summary>Gets or sets the slot registered under <paramref name="name"/>.</summary>
    /// <param name="name">The slot name.</param>
    /// <returns>The registered slot.</returns>
    public ComponentSlot this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return _slots[name];
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);
            _slots[name] = value;
        }
    }

    /// <summary>Removes the slot registered under <paramref name="name"/>.</summary>
    /// <param name="name">The slot name.</param>
    /// <returns>True when a slot was removed.</returns>
    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _slots.Remove(name);
    }

    /// <inheritdoc/>
    public bool ContainsKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _slots.ContainsKey(key);
    }

    /// <inheritdoc/>
    public bool TryGetValue(string key, out ComponentSlot value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _slots.TryGetValue(key, out value!);
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, ComponentSlot>> GetEnumerator() =>
        _slots.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
