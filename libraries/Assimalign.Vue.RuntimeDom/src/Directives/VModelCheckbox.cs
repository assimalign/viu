using System;
using System.Collections;
using System.Collections.Generic;

using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The <c>v-model</c> directive for checkboxes — the C# port of upstream's <c>vModelCheckbox</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
/// https://vuejs.org/guide/essentials/forms.html#checkbox). Binds a boolean (honoring
/// <c>true-value</c>/<c>false-value</c>), or adds/removes the element's bound <c>:value</c> in a
/// model <see cref="IList"/> (loose-equality membership, upstream <c>looseIndexOf</c>) or
/// <see cref="ISet{T}"/> (strict membership, upstream <c>Set.has</c>). The bound <c>:value</c>,
/// <c>true-value</c>, and <c>false-value</c> are read as raw vnode props, so object values
/// round-trip. Reads <c>checked</c> from the dispatched <see cref="BrowserEvent"/> payload, never a
/// follow-up interop read. Stateless singleton (<see cref="Instance"/>); per-element state lives in
/// <see cref="BrowserModelState"/>.
/// </summary>
public sealed class VModelCheckbox : IDirective
{
    /// <summary>The shared directive instance the compiler references.</summary>
    public static readonly VModelCheckbox Instance = new();

    private static readonly object BoxedTrue = true;
    private static readonly object BoxedFalse = false;

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

    private static void OnCreated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        operations.GetState(handle).Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        operations.SetModelListener(handle, "onChange", (Action<BrowserEvent>)(browserEvent => OnChange(operations, handle, browserEvent)));
    }

    private static void OnMounted(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => SetChecked(BrowserDirectiveOperations.Require(), BrowserModelDirective.Handle(element), binding, node);

    private static void OnBeforeUpdate(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        operations.GetState(handle).Assign = BrowserModelDirective.Carrier(binding)?.Setter; // refresh assigner
        SetChecked(operations, handle, binding, node);
    }

    private static void OnBeforeUnmount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => BrowserDirectiveOperations.Require().ReleaseState(BrowserModelDirective.Handle(element));

    private static void OnChange(BrowserDirectiveOperations operations, int handle, BrowserEvent browserEvent)
    {
        var state = operations.GetState(handle);
        var assign = state.Assign;
        if (assign is null)
        {
            return;
        }
        var model = state.ModelValue;
        var elementValue = state.ElementValue;
        var isChecked = browserEvent.TargetChecked;
        if (BrowserModelDirective.IsList(model))
        {
            var list = (IList)model!;
            var index = LooseEquality.LooseIndexOf(list, elementValue);
            var found = index != -1;
            if (isChecked && !found)
            {
                var next = BrowserModelDirective.CopyToList(list);
                next.Add(elementValue);
                assign(next);
            }
            else if (!isChecked && found)
            {
                var next = BrowserModelDirective.CopyToList(list);
                next.RemoveAt(index);
                assign(next);
            }
        }
        else if (BrowserModelDirective.IsSet(model))
        {
            var next = BrowserModelDirective.CopyToSet((IEnumerable)model!);
            if (isChecked)
            {
                next.Add(elementValue);
            }
            else
            {
                next.Remove(elementValue);
            }
            assign(next);
        }
        else
        {
            // Boolean binding: checked -> true-value (or true), unchecked -> false-value (or false).
            assign(isChecked ? state.TrueValue : state.FalseValue);
        }
    }

    // Reflect the model onto el.checked and refresh the change handler's inputs (upstream setChecked).
    private static void SetChecked(BrowserDirectiveOperations operations, int handle, DirectiveBinding binding, VirtualNode node)
    {
        var state = operations.GetState(handle);
        var value = BrowserModelDirective.Carrier(binding)?.Value;
        state.ElementValue = BrowserModelDirective.Property(node, "value");
        state.TrueValue = node.Properties?.ContainsName("true-value") == true
            ? BrowserModelDirective.Property(node, "true-value")
            : BoxedTrue;
        state.FalseValue = node.Properties?.ContainsName("false-value") == true
            ? BrowserModelDirective.Property(node, "false-value")
            : BoxedFalse;
        state.ModelValue = value;

        bool isChecked;
        if (BrowserModelDirective.IsList(value))
        {
            isChecked = LooseEquality.LooseIndexOf((IList)value!, state.ElementValue) > -1;
        }
        else if (BrowserModelDirective.IsSet(value))
        {
            isChecked = BrowserModelDirective.SetContains((IEnumerable)value!, state.ElementValue);
        }
        else
        {
            // Upstream: if (value === oldValue) return — no change to reflect.
            var oldValue = BrowserModelDirective.ModelValue(binding.OldValue);
            if (Equals(value, oldValue))
            {
                return;
            }
            isChecked = LooseEquality.LooseEqual(value, state.TrueValue);
        }
        operations.SetBooleanProperty(handle, "checked", isChecked);
    }
}
