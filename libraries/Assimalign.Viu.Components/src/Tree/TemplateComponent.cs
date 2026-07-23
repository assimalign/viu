using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>An immutable request to activate and mount a component template.</summary>
public sealed class TemplateComponent : ITemplateComponent
{
    /// <summary>Creates a template component request.</summary>
    /// <param name="templateType">The explicitly registered template type.</param>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="listeners">The parent listeners for component-emitted events.</param>
    /// <param name="directives">The directives applied to the template's rendered root.</param>
    /// <param name="reference">The optional template-reference binding.</param>
    public TemplateComponent(
        Type templateType,
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
    {
        ArgumentNullException.ThrowIfNull(templateType);
        TemplateType = templateType;
        Arguments = arguments ?? new ComponentArguments();
        Slots = CopySlots(slots);
        Listeners = ComponentEventListeners.Copy(listeners);
        Key = key;
        Optimization = optimization ?? ComponentOptimization.None;
        Directives = ComponentDirectiveBindings.Copy(directives);
        Reference = reference;
    }

    /// <summary>Creates a named template component request without activating the template.</summary>
    /// <param name="templateName">The registered template name.</param>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="listeners">The parent listeners for component-emitted events.</param>
    /// <param name="directives">The directives applied to the template's rendered root.</param>
    /// <param name="reference">The optional template-reference binding.</param>
    public TemplateComponent(
        string templateName,
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateName);
        TemplateName = templateName;
        Arguments = arguments ?? new ComponentArguments();
        Slots = CopySlots(slots);
        Listeners = ComponentEventListeners.Copy(listeners);
        Key = key;
        Optimization = optimization ?? ComponentOptimization.None;
        Directives = ComponentDirectiveBindings.Copy(directives);
        Reference = reference;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Template;

    /// <inheritdoc/>
    public object? Key { get; }

    /// <inheritdoc/>
    public IComponentReference? Reference { get; }

    /// <inheritdoc/>
    public ComponentOptimization Optimization { get; }

    /// <inheritdoc/>
    public Type? TemplateType { get; }

    /// <inheritdoc/>
    public string? TemplateName { get; }

    /// <inheritdoc/>
    public IComponentArguments Arguments { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ComponentSlot>? Slots { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ComponentEventListener>? Listeners { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IComponentDirectiveBinding> Directives { get; }

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

        return new ComponentSlotCollection(
            snapshot,
            slots is IComponentSlotCollection componentSlots
                ? componentSlots.Flags
                : Assimalign.Viu.Shared.SlotFlags.Stable);
    }
}
