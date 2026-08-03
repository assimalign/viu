using System;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The <c>v-model</c> directive for elements whose concrete model directive is selected from the
/// current element tag and input type at runtime.
/// </summary>
/// <remarks>
/// Used where the compiler cannot prove which model directive applies — a dynamic <c>:type</c>, or a
/// <c>&lt;component :is&gt;</c> that resolves to an element tag. Each hook re-resolves the concrete
/// directive from the element's current tag and type and forwards to it, so a control that changes
/// type between renders is torn down and re-bound with the right semantics rather than keeping the
/// first one it happened to match.
/// </remarks>
public sealed class VModelDynamic : IDirective
{
    /// <summary>The shared directive instance the compiler references.</summary>
    public static readonly VModelDynamic Instance = new();

    private VModelDynamic()
    {
    }

    /// <inheritdoc/>
    public DirectiveHook? Created => OnCreated;

    /// <inheritdoc/>
    public DirectiveHook? Mounted => OnMounted;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUpdate => OnBeforeUpdate;

    /// <inheritdoc/>
    public DirectiveHook? Updated => OnUpdated;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUnmount => OnBeforeUnmount;

    private static void OnCreated(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        Resolve(component).Created?.Invoke(
            element,
            binding,
            component,
            previousComponent);
    }

    private static void OnMounted(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        Resolve(component).Mounted?.Invoke(
            element,
            binding,
            component,
            previousComponent);
    }

    private static void OnBeforeUpdate(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        Resolve(component).BeforeUpdate?.Invoke(
            element,
            binding,
            component,
            previousComponent);
    }

    private static void OnUpdated(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        Resolve(component).Updated?.Invoke(
            element,
            binding,
            component,
            previousComponent);
    }

    private static void OnBeforeUnmount(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        Resolve(component).BeforeUnmount?.Invoke(
            element,
            binding,
            component,
            previousComponent);
    }

    private static IDirective Resolve(IElementComponent component)
    {
        if (string.Equals(
            component.Tag,
            "select",
            StringComparison.OrdinalIgnoreCase))
        {
            return VModelSelect.Instance;
        }

        if (string.Equals(
            component.Tag,
            "textarea",
            StringComparison.OrdinalIgnoreCase))
        {
            return VModelText.Instance;
        }

        return BrowserModelDirective.FormatValue(
            BrowserModelDirective.Property(component, "type")) switch
        {
            "checkbox" => VModelCheckbox.Instance,
            "radio" => VModelRadio.Instance,
            _ => VModelText.Instance,
        };
    }
}
