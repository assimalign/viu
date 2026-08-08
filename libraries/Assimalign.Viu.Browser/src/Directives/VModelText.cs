using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Provides Browser text-control two-way model behavior.</summary>
/// <remarks>
/// The directive reflects the current model into <c>value</c>, writes input through the generated
/// setter, suppresses incomplete IME composition, and honors <c>lazy</c>, <c>trim</c>, and
/// <c>number</c>. It is a qualified public type token and reusable singleton; per-element state is
/// host-owned. Specified by <c>[SFC-CG-6]</c> and <c>[SFC-CG-7]</c>.
/// </remarks>
public sealed class VModelText : IDirective
{
    /// <summary>Gets the reusable Browser text-model directive.</summary>
    public static VModelText Instance { get; } = new();

    private VModelText()
    {
    }

    /// <inheritdoc/>
    public DirectiveHook? Created => OnCreated;

    /// <inheritdoc/>
    public DirectiveHook? Mounted => OnMounted;

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
        operations.GetState(handle).Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        ConfigureListeners(operations, handle, binding, value);
    }

    private static void OnMounted(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        string formatted = BrowserModelDirective.FormatValue(
            BrowserModelDirective.Carrier(binding)?.Value);
        operations.SetValueGuarded(handle, formatted);
        operations.GetState(handle).CurrentValue = formatted;
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
        ConfigureListeners(operations, handle, binding, value);
        if (state.Composing)
        {
            return;
        }

        object? model = BrowserModelDirective.Carrier(binding)?.Value;
        string nextValue = BrowserModelDirective.FormatValue(model);
        if (string.Equals(state.CurrentValue, nextValue, StringComparison.Ordinal))
        {
            return;
        }

        if (state.Focused)
        {
            if (BrowserModelDirective.HasModifier(binding, "lazy")
                && LooseEquality.LooseEqual(
                    model,
                    BrowserModelDirective.ModelValue(binding.PreviousValue)))
            {
                return;
            }

            if (BrowserModelDirective.HasModifier(binding, "trim")
                && string.Equals(
                    state.CurrentValue.Trim(),
                    nextValue,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        operations.SetValueGuarded(handle, nextValue);
        state.CurrentValue = nextValue;
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

    private static void ConfigureListeners(
        BrowserDirectiveOperations operations,
        int handle,
        DirectiveBinding binding,
        ElementNode value)
    {
        bool lazy = BrowserModelDirective.HasModifier(binding, "lazy");
        bool trim = BrowserModelDirective.HasModifier(binding, "trim");
        bool castToNumber = BrowserModelDirective.HasModifier(binding, "number")
            || BrowserModelDirective.IsNumberType(value);

        operations.SetModelListener(handle, "onInput", lazy
            ? null
            : (Action<BrowserEvent>)(browserEvent =>
                Commit(operations, handle, browserEvent, trim, castToNumber)));
        operations.SetModelListener(handle, "onCompositionstart", lazy
            ? null
            : (Action<BrowserEvent>)(_ => operations.GetState(handle).Composing = true));
        operations.SetModelListener(handle, "onCompositionend", lazy
            ? null
            : (Action<BrowserEvent>)(browserEvent =>
                OnCompositionEnd(operations, handle, browserEvent, trim, castToNumber)));
        operations.SetModelListener(
            handle,
            "onChange",
            (Action<BrowserEvent>)(browserEvent =>
                OnChange(operations, handle, browserEvent, lazy, trim, castToNumber)));
        operations.SetModelListener(
            handle,
            "onFocus",
            (Action)(() => operations.GetState(handle).Focused = true));
        operations.SetModelListener(
            handle,
            "onBlur",
            (Action)(() => operations.GetState(handle).Focused = false));
    }

    private static void Commit(
        BrowserDirectiveOperations operations,
        int handle,
        BrowserEvent browserEvent,
        bool trim,
        bool castToNumber)
    {
        BrowserModelState state = operations.GetState(handle);
        if (state.Composing)
        {
            return;
        }

        string browserValue = browserEvent.TargetValue ?? string.Empty;
        state.CurrentValue = browserValue;
        string candidate = trim ? browserValue.Trim() : browserValue;
        state.Assign?.Invoke(castToNumber
            ? NumberCoercion.LooseToNumber(candidate)
            : candidate);
    }

    private static void OnChange(
        BrowserDirectiveOperations operations,
        int handle,
        BrowserEvent browserEvent,
        bool lazy,
        bool trim,
        bool castToNumber)
    {
        BrowserModelState state = operations.GetState(handle);
        if (lazy)
        {
            Commit(operations, handle, browserEvent, trim, castToNumber);
        }
        else if (state.Composing)
        {
            state.Composing = false;
            Commit(operations, handle, browserEvent, trim, castToNumber);
        }

        if (trim)
        {
            string trimmed = (browserEvent.TargetValue ?? string.Empty).Trim();
            state.CurrentValue = trimmed;
            operations.SetValueGuarded(handle, trimmed);
        }
    }

    private static void OnCompositionEnd(
        BrowserDirectiveOperations operations,
        int handle,
        BrowserEvent browserEvent,
        bool trim,
        bool castToNumber)
    {
        BrowserModelState state = operations.GetState(handle);
        if (!state.Composing)
        {
            return;
        }

        state.Composing = false;
        Commit(operations, handle, browserEvent, trim, castToNumber);
    }
}
