using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu.Components;

/// <summary>An immutable request to activate and mount a component template.</summary>
public sealed class TemplateComponent : ITemplateComponent
{
    /// <summary>Creates a template component request.</summary>
    /// <param name="templateType">The explicitly registered template type.</param>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    public TemplateComponent(
        Type templateType,
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null)
    {
        ArgumentNullException.ThrowIfNull(templateType);
        TemplateType = templateType;
        Arguments = arguments ?? new ComponentArguments();
        Slots = CopySlots(slots);
        Key = key;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Template;

    /// <inheritdoc/>
    public object? Key { get; }

    /// <inheritdoc/>
    public Type TemplateType { get; }

    /// <inheritdoc/>
    public IComponentArguments Arguments { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ComponentSlot>? Slots { get; }

    private static IReadOnlyDictionary<string, ComponentSlot>? CopySlots(
        IReadOnlyDictionary<string, ComponentSlot>? slots)
    {
        if (slots is null || slots.Count == 0)
        {
            return null;
        }

        Dictionary<string, ComponentSlot> snapshot = new(slots.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, ComponentSlot> slot in slots)
        {
            ArgumentException.ThrowIfNullOrEmpty(slot.Key);
            ArgumentNullException.ThrowIfNull(slot.Value);
            snapshot.Add(slot.Key, slot.Value);
        }

        return new ReadOnlyDictionary<string, ComponentSlot>(snapshot);
    }
}

