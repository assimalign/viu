using System;
using System.Collections;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Provides Browser checkbox two-way model behavior.</summary>
/// <remarks>
/// Scalar models honor raw <c>true-value</c>/<c>false-value</c>; list and set models receive a
/// newly assigned collection containing the raw bound <c>value</c>. Browser event payloads supply
/// checked state without a follow-up interop read. Specified by <c>[SFC-CG-6]</c> and
/// <c>[SFC-CG-7]</c>.
/// </remarks>
public sealed class VModelCheckbox : IDirective
{
    private static readonly object BoxedTrue = true;
    private static readonly object BoxedFalse = false;

    /// <summary>Gets the reusable Browser checkbox-model directive.</summary>
    public static VModelCheckbox Instance { get; } = new();

    private VModelCheckbox()
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
        operations.SetModelListener(
            handle,
            "onChange",
            (Action<BrowserEvent>)(browserEvent =>
                OnChange(operations, handle, browserEvent)));
    }

    private static void OnMounted(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        SetChecked(
            BrowserDirectiveOperations.Require(),
            BrowserModelDirective.Handle(element),
            binding,
            value);
    }

    private static void OnBeforeUpdate(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        operations.GetState(handle).Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        SetChecked(operations, handle, binding, value);
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

    private static void OnChange(
        BrowserDirectiveOperations operations,
        int handle,
        BrowserEvent browserEvent)
    {
        BrowserModelState state = operations.GetState(handle);
        Action<object?>? assign = state.Assign;
        if (assign is null)
        {
            return;
        }

        if (BrowserModelDirective.IsList(state.ModelValue))
        {
            IList list = (IList)state.ModelValue!;
            int index = LooseEquality.LooseIndexOf(list, state.ElementValue);
            if (browserEvent.TargetChecked && index < 0)
            {
                var next = BrowserModelDirective.CopyToList(list);
                next.Add(state.ElementValue);
                assign(next);
            }
            else if (!browserEvent.TargetChecked && index >= 0)
            {
                var next = BrowserModelDirective.CopyToList(list);
                next.RemoveAt(index);
                assign(next);
            }

            return;
        }

        if (BrowserModelDirective.IsSet(state.ModelValue))
        {
            var next = BrowserModelDirective.CopyToSet((IEnumerable)state.ModelValue!);
            if (browserEvent.TargetChecked)
            {
                next.Add(state.ElementValue);
            }
            else
            {
                next.Remove(state.ElementValue);
            }

            assign(next);
            return;
        }

        assign(browserEvent.TargetChecked ? state.TrueValue : state.FalseValue);
    }

    private static void SetChecked(
        BrowserDirectiveOperations operations,
        int handle,
        DirectiveBinding binding,
        ElementNode value)
    {
        BrowserModelState state = operations.GetState(handle);
        object? model = BrowserModelDirective.Carrier(binding)?.Value;
        state.ElementValue = BrowserModelDirective.Property(value, "value");
        state.TrueValue = BrowserModelDirective.HasProperty(value, "true-value")
            ? BrowserModelDirective.Property(value, "true-value")
            : BoxedTrue;
        state.FalseValue = BrowserModelDirective.HasProperty(value, "false-value")
            ? BrowserModelDirective.Property(value, "false-value")
            : BoxedFalse;
        state.ModelValue = model;

        bool isChecked;
        if (BrowserModelDirective.IsList(model))
        {
            isChecked = LooseEquality.LooseIndexOf((IList)model, state.ElementValue) >= 0;
        }
        else if (BrowserModelDirective.IsSet(model))
        {
            isChecked = BrowserModelDirective.SetContains(
                (IEnumerable)model,
                state.ElementValue);
        }
        else
        {
            isChecked = LooseEquality.LooseEqual(model, state.TrueValue);
        }

        operations.SetBooleanProperty(handle, "checked", isChecked);
    }
}
