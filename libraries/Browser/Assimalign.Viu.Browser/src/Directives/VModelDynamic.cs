using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Selects Browser model behavior from the current element name and input type.</summary>
/// <remarks>
/// Generated code uses this qualified public token only when the concrete native control cannot
/// be proven at compile time. Each lifecycle phase re-resolves the current immutable element.
/// File inputs fail because browsers prohibit programmatic two-way value binding. Specified by
/// <c>[SFC-CG-6]</c> and <c>[SFC-CG-7]</c>.
/// </remarks>
public sealed class VModelDynamic : IDirective
{
    /// <summary>Gets the reusable Browser dynamic-model directive.</summary>
    public static VModelDynamic Instance { get; } = new();

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
        ElementNode value,
        ElementNode? previousValue)
    {
        IDirective directive = Resolve(value);
        directive.Created?.Invoke(element, binding, value, previousValue);
        BrowserDirectiveOperations.Require()
            .GetState(BrowserModelDirective.Handle(element))
            .DynamicDirective = directive;
    }

    private static void OnMounted(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        BrowserModelState state = operations.GetState(BrowserModelDirective.Handle(element));
        IDirective directive = state.DynamicDirective ?? Resolve(value);
        directive.Mounted?.Invoke(element, binding, value, previousValue);
        operations.GetState(BrowserModelDirective.Handle(element)).DynamicDirective = directive;
    }

    private static void OnBeforeUpdate(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        BrowserModelState state = operations.GetState(handle);
        IDirective nextDirective = Resolve(value);
        IDirective currentDirective = state.DynamicDirective
            ?? (previousValue is null ? nextDirective : Resolve(previousValue));
        if (ReferenceEquals(currentDirective, nextDirective))
        {
            nextDirective.BeforeUpdate?.Invoke(element, binding, value, previousValue);
            operations.GetState(handle).DynamicDirective = nextDirective;
            return;
        }

        currentDirective.BeforeUnmount?.Invoke(element, binding, value, previousValue);
        BrowserModelDirective.ClearModelListeners(operations, handle);
        nextDirective.Created?.Invoke(element, binding, value, previousValue);
        BrowserModelState nextState = operations.GetState(handle);
        nextState.DynamicDirective = nextDirective;
        nextState.DynamicMountPending = true;
    }

    private static void OnUpdated(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        BrowserModelState state = operations.GetState(handle);
        IDirective directive = state.DynamicDirective ?? Resolve(value);
        if (state.DynamicMountPending)
        {
            state.DynamicMountPending = false;
            directive.Mounted?.Invoke(element, binding, value, previousValue);
            operations.GetState(handle).DynamicDirective = directive;
            return;
        }

        directive.Updated?.Invoke(element, binding, value, previousValue);
    }

    private static void OnBeforeUnmount(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        IDirective directive = operations.GetState(handle).DynamicDirective ?? Resolve(value);
        directive.BeforeUnmount?.Invoke(element, binding, value, previousValue);
        BrowserModelDirective.ClearModelListeners(operations, handle);
    }

    private static IDirective Resolve(ElementNode value)
    {
        if (string.Equals(
            value.Name.LocalName,
            "select",
            StringComparison.OrdinalIgnoreCase))
        {
            return VModelSelect.Instance;
        }

        if (string.Equals(
            value.Name.LocalName,
            "textarea",
            StringComparison.OrdinalIgnoreCase))
        {
            return VModelText.Instance;
        }

        string inputType = BrowserModelDirective.FormatValue(
            BrowserModelDirective.Property(value, "type"));
        if (string.Equals(inputType, "file", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "v-model cannot be used on a file input; consume its change event instead.");
        }

        if (string.Equals(inputType, "checkbox", StringComparison.OrdinalIgnoreCase))
        {
            return VModelCheckbox.Instance;
        }

        return string.Equals(inputType, "radio", StringComparison.OrdinalIgnoreCase)
            ? VModelRadio.Instance
            : VModelText.Instance;
    }
}
