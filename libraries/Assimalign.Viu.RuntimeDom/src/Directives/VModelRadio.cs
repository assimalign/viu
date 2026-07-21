using System;

using Assimalign.Viu;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// The <c>v-model</c> directive for radio buttons — the C# port of upstream's <c>vModelRadio</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
/// https://vuejs.org/guide/essentials/forms.html#radio). The element is <c>checked</c> when the
/// model loosely equals the radio's bound <c>:value</c> (upstream <c>looseEqual</c>), so object
/// values round-trip without string coercion; on change the model is assigned the radio's raw
/// <c>:value</c>. Stateless singleton (<see cref="Instance"/>); per-element state lives in
/// <see cref="BrowserModelState"/>.
/// </summary>
public sealed class VModelRadio : IDirective
{
    /// <summary>The shared directive instance the compiler references.</summary>
    public static readonly VModelRadio Instance = new();

    private VModelRadio()
    {
    }

    /// <inheritdoc/>
    public DirectiveHook? Created => OnCreated;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUpdate => OnBeforeUpdate;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUnmount => OnBeforeUnmount;

    private static void OnCreated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        var state = operations.GetState(handle);
        state.ElementValue = BrowserModelDirective.Property(node, "value");
        state.Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        // Upstream: el.checked = looseEqual(value, vnode.props.value).
        operations.SetBooleanProperty(handle, "checked",
            LooseEquality.LooseEqual(BrowserModelDirective.Carrier(binding)?.Value, state.ElementValue));
        operations.SetModelListener(handle, "onChange",
            (Action)(() => operations.GetState(handle).Assign?.Invoke(operations.GetState(handle).ElementValue)));
    }

    private static void OnBeforeUpdate(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        var state = operations.GetState(handle);
        state.Assign = BrowserModelDirective.Carrier(binding)?.Setter; // refresh assigner
        state.ElementValue = BrowserModelDirective.Property(node, "value");
        var value = BrowserModelDirective.Carrier(binding)?.Value;
        // Upstream: if (value !== oldValue) el.checked = looseEqual(value, vnode.props.value).
        if (!Equals(value, BrowserModelDirective.ModelValue(binding.OldValue)))
        {
            operations.SetBooleanProperty(handle, "checked", LooseEquality.LooseEqual(value, state.ElementValue));
        }
    }

    private static void OnBeforeUnmount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => BrowserDirectiveOperations.Require().ReleaseState(BrowserModelDirective.Handle(element));
}
