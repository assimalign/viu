using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Provides Browser radio-control two-way model behavior.</summary>
/// <remarks>
/// Checked state uses form-value loose equality while change assigns the raw bound
/// <c>value</c>, preserving object identity without reflection. Specified by <c>[SFC-CG-6]</c>
/// and <c>[SFC-CG-7]</c>.
/// </remarks>
public sealed class VModelRadio : IDirective
{
    /// <summary>Gets the reusable Browser radio-model directive.</summary>
    public static VModelRadio Instance { get; } = new();

    private VModelRadio()
    {
    }

    /// <inheritdoc/>
    public DirectiveHook? Created => OnCreated;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUpdate => OnBeforeUpdate;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUnmount => OnBeforeUnmount;

    private static void OnCreated(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        BrowserModelState state = operations.GetState(handle);
        state.ElementValue = BrowserModelDirective.Property(value, "value");
        state.Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        operations.SetBooleanProperty(
            handle,
            "checked",
            LooseEquality.LooseEqual(
                BrowserModelDirective.Carrier(binding)?.Value,
                state.ElementValue));
        operations.SetModelListener(
            handle,
            "onChange",
            (Action)(() =>
            {
                BrowserModelState current = operations.GetState(handle);
                current.Assign?.Invoke(current.ElementValue);
            }));
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
        state.Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        state.ElementValue = BrowserModelDirective.Property(value, "value");
        object? model = BrowserModelDirective.Carrier(binding)?.Value;
        object? previousElementValue = previousValue is null
            ? null
            : BrowserModelDirective.Property(previousValue, "value");
        if (!Equals(model, BrowserModelDirective.ModelValue(binding.PreviousValue))
            || !Equals(state.ElementValue, previousElementValue))
        {
            operations.SetBooleanProperty(
                handle,
                "checked",
                LooseEquality.LooseEqual(model, state.ElementValue));
        }
    }

    private static void OnBeforeUnmount(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations.Require().ReleaseState(
            BrowserModelDirective.Handle(element));
    }
}
