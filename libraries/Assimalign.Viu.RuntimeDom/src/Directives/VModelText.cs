using System;

using Assimalign.Viu.RuntimeCore;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// The <c>v-model</c> directive for text inputs and <c>&lt;textarea&gt;</c> — the C# port of
/// upstream's <c>vModelText</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
/// https://vuejs.org/guide/essentials/forms.html). Reflects the model onto <c>el.value</c> and
/// writes user edits back through the binding's <see cref="ViuModelBinding.Setter"/>. IME safety:
/// input updates are suppressed between <c>compositionstart</c> and <c>compositionend</c>. Modifiers:
/// <c>.lazy</c> listens on <c>change</c> instead of <c>input</c>; <c>.number</c> coerces via
/// <see cref="NumberCoercion.LooseToNumber(object?)"/> (non-numeric input is left untouched);
/// <c>.trim</c> trims the bound value and re-syncs the element on <c>change</c> (blur). Reads the DOM
/// value from the dispatched <see cref="BrowserEvent"/> payload, never a follow-up interop read.
/// Stateless singleton (<see cref="Instance"/>) referenced by the compiled <c>v-model</c> transform
/// ([V01.01.05.03]); per-element state lives in <see cref="BrowserModelState"/>.
/// </summary>
public sealed class VModelText : IDirective
{
    /// <summary>The shared directive instance the compiler references.</summary>
    public static readonly VModelText Instance = new();

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

    private static void OnCreated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        operations.GetState(handle).Assign = BrowserModelDirective.Carrier(binding)?.Setter;

        var lazy = BrowserModelDirective.HasModifier(binding, "lazy");
        var trim = BrowserModelDirective.HasModifier(binding, "trim");
        var castToNumber = BrowserModelDirective.HasModifier(binding, "number")
            || BrowserModelDirective.IsNumberType(node);

        if (lazy)
        {
            operations.SetModelListener(handle, "onChange",
                (Action<BrowserEvent>)(browserEvent => OnChange(operations, handle, browserEvent, lazy: true, trim, castToNumber)));
        }
        else
        {
            operations.SetModelListener(handle, "onInput",
                (Action<BrowserEvent>)(browserEvent => Commit(operations, handle, browserEvent, trim, castToNumber)));
            operations.SetModelListener(handle, "onCompositionstart",
                (Action<BrowserEvent>)(_ => operations.GetState(handle).Composing = true));
            operations.SetModelListener(handle, "onCompositionend",
                (Action<BrowserEvent>)(browserEvent => OnCompositionEnd(operations, handle, browserEvent, trim, castToNumber)));
            // Upstream also binds onCompositionEnd (and the trim re-sync) to 'change'.
            operations.SetModelListener(handle, "onChange",
                (Action<BrowserEvent>)(browserEvent => OnChange(operations, handle, browserEvent, lazy: false, trim, castToNumber)));
        }

        // Focus tracking gates the lazy/trim write guards below without an activeElement interop
        // read. A listener on the element fires for its own (non-bubbling) focus/blur in the target
        // phase; real browser focus is exercised by the e2e harness ([V01.01.11.03]).
        operations.SetModelListener(handle, "onFocus", (Action)(() => operations.GetState(handle).Focused = true));
        operations.SetModelListener(handle, "onBlur", (Action)(() => operations.GetState(handle).Focused = false));
    }

    private static void OnMounted(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        // Upstream: el.value = value == null ? '' : value.
        var formatted = BrowserModelDirective.FormatValue(BrowserModelDirective.Carrier(binding)?.Value);
        operations.SetValueGuarded(handle, formatted);
        operations.GetState(handle).CurrentValue = formatted;
    }

    private static void OnBeforeUpdate(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        var state = operations.GetState(handle);
        state.Assign = BrowserModelDirective.Carrier(binding)?.Setter; // refresh the assigner (upstream)
        if (state.Composing)
        {
            return; // never clobber an in-progress composition
        }
        var model = BrowserModelDirective.Carrier(binding)?.Value;
        var newValue = BrowserModelDirective.FormatValue(model);
        // Primary guard: the DOM already shows the model (also protects a focused caret).
        if (string.Equals(state.CurrentValue, newValue, StringComparison.Ordinal))
        {
            return;
        }
        if (state.Focused)
        {
            // Upstream gates these on document.activeElement === el && el.type !== 'range'; a range
            // input's guard nuance is deferred (uncommon; e2e harness [V01.01.11.03]).
            var lazy = BrowserModelDirective.HasModifier(binding, "lazy");
            var trim = BrowserModelDirective.HasModifier(binding, "trim");
            if (lazy && LooseEquality.LooseEqual(model, BrowserModelDirective.ModelValue(binding.OldValue)))
            {
                return;
            }
            if (trim && string.Equals(state.CurrentValue.Trim(), newValue, StringComparison.Ordinal))
            {
                return;
            }
        }
        operations.SetValueGuarded(handle, newValue);
        state.CurrentValue = newValue;
    }

    private static void OnBeforeUnmount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => BrowserDirectiveOperations.Require().ReleaseState(BrowserModelDirective.Handle(element));

    // The DOM value -> model commit shared by input/change/compositionend (upstream: the shared
    // listener body). trim then number, matching upstream order.
    private static void Commit(BrowserDirectiveOperations operations, int handle, BrowserEvent browserEvent, bool trim, bool castToNumber)
    {
        var state = operations.GetState(handle);
        if (state.Composing)
        {
            return;
        }
        var domValue = browserEvent.TargetValue ?? string.Empty;
        state.CurrentValue = domValue;
        var candidate = trim ? domValue.Trim() : domValue;
        var modelValue = castToNumber ? NumberCoercion.LooseToNumber(candidate) : candidate;
        state.Assign?.Invoke(modelValue);
    }

    private static void OnChange(BrowserDirectiveOperations operations, int handle, BrowserEvent browserEvent, bool lazy, bool trim, bool castToNumber)
    {
        var state = operations.GetState(handle);
        if (lazy)
        {
            Commit(operations, handle, browserEvent, trim, castToNumber); // .lazy commits on change
        }
        else if (state.Composing)
        {
            // A composition can be ended by 'change' too (upstream binds onCompositionEnd here).
            state.Composing = false;
            Commit(operations, handle, browserEvent, trim, castToNumber);
        }
        if (trim)
        {
            // Re-sync the element to the trimmed value on blur/change (upstream trim 'change' hook).
            var trimmed = (browserEvent.TargetValue ?? string.Empty).Trim();
            state.CurrentValue = trimmed;
            operations.SetValueGuarded(handle, trimmed);
        }
    }

    private static void OnCompositionEnd(BrowserDirectiveOperations operations, int handle, BrowserEvent browserEvent, bool trim, bool castToNumber)
    {
        var state = operations.GetState(handle);
        if (!state.Composing)
        {
            return;
        }
        state.Composing = false;
        Commit(operations, handle, browserEvent, trim, castToNumber);
    }
}
