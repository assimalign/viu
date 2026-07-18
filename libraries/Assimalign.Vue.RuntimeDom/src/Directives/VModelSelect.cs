using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The <c>v-model</c> directive for <c>&lt;select&gt;</c> — the C# port of upstream's
/// <c>vModelSelect</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/directives/vModel.ts,
/// https://vuejs.org/guide/essentials/forms.html#select). Reflects the model onto the options'
/// selected state and, on change, assigns the selected value(s) back: a single select assigns one
/// value, a <c>multiple</c> select assigns a list or (when the model is a set) a set. Option values
/// compare by loose equality (upstream <c>looseEqual</c>). Object option values round-trip: the
/// directive snapshots each option's raw <c>:value</c> and maps the dispatched selection strings
/// (single: <see cref="BrowserEvent.TargetValue"/>; multiple: <see cref="BrowserEvent.SelectedValues"/>)
/// back to the raw value — never a follow-up interop read. Stateless singleton
/// (<see cref="Instance"/>); per-element state lives in <see cref="BrowserModelState"/>.
/// </summary>
public sealed class VModelSelect : IDirective
{
    /// <summary>The shared directive instance the compiler references.</summary>
    public static readonly VModelSelect Instance = new();

    private VModelSelect()
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

    private static void OnCreated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        operations.GetState(handle).Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        // Upstream captures isSetModel and reads number once at created; multiple is captured here
        // (a runtime :multiple toggle is uncommon and deferred).
        var multiple = IsMultiple(node);
        var isSetModel = BrowserModelDirective.IsSet(BrowserModelDirective.Carrier(binding)?.Value);
        var castToNumber = BrowserModelDirective.HasModifier(binding, "number");
        operations.SetModelListener(handle, "onChange",
            (Action<BrowserEvent>)(browserEvent => OnChange(operations, handle, browserEvent, multiple, isSetModel, castToNumber)));
    }

    private static void OnMounted(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => SetSelected(BrowserDirectiveOperations.Require(), BrowserModelDirective.Handle(element), node, BrowserModelDirective.Carrier(binding)?.Value);

    private static void OnBeforeUpdate(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => BrowserDirectiveOperations.Require().GetState(BrowserModelDirective.Handle(element)).Assign
            = BrowserModelDirective.Carrier(binding)?.Setter;

    private static void OnUpdated(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
    {
        var operations = BrowserDirectiveOperations.Require();
        var handle = BrowserModelDirective.Handle(element);
        var state = operations.GetState(handle);
        // Upstream: if (!el._assigning) setSelected(...). Skip the reflect that would re-run right
        // after the user's own change; consume the flag (a faithful simplification of the nextTick
        // clear — see BrowserModelState).
        if (state.Assigning)
        {
            state.Assigning = false;
            return;
        }
        SetSelected(operations, handle, node, BrowserModelDirective.Carrier(binding)?.Value);
    }

    private static void OnBeforeUnmount(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode)
        => BrowserDirectiveOperations.Require().ReleaseState(BrowserModelDirective.Handle(element));

    private static void OnChange(BrowserDirectiveOperations operations, int handle, BrowserEvent browserEvent, bool multiple, bool isSetModel, bool castToNumber)
    {
        var state = operations.GetState(handle);
        var assign = state.Assign;
        if (assign is null)
        {
            return;
        }
        if (multiple)
        {
            var selected = new List<object?>();
            if (browserEvent.SelectedValues is { } selectedValues)
            {
                foreach (var domValue in selectedValues)
                {
                    selected.Add(MapSelected(state.OptionValues, domValue, castToNumber));
                }
            }
            if (isSetModel)
            {
                var set = new HashSet<object?>();
                foreach (var value in selected)
                {
                    set.Add(value);
                }
                assign(set);
            }
            else
            {
                assign(selected);
            }
        }
        else
        {
            assign(MapSelected(state.OptionValues, browserEvent.TargetValue ?? string.Empty, castToNumber));
        }
        state.Assigning = true;
    }

    // Reflect the model onto the options' selected state and refresh the option-value snapshot
    // (upstream setSelected).
    private static void SetSelected(BrowserDirectiveOperations operations, int handle, VirtualNode node, object? value)
    {
        var options = new List<(int Handle, object? RawValue)>();
        CollectOptions(node, options);

        var snapshot = new List<KeyValuePair<string, object?>>(options.Count);
        foreach (var (_, rawValue) in options)
        {
            snapshot.Add(new KeyValuePair<string, object?>(BrowserModelDirective.FormatValue(rawValue), rawValue));
        }
        operations.GetState(handle).OptionValues = snapshot;

        var multiple = IsMultiple(node);
        var isArrayValue = BrowserModelDirective.IsList(value);
        var isSetValue = BrowserModelDirective.IsSet(value);
        if (multiple && !isArrayValue && !isSetValue)
        {
            // Upstream warns and bails when a multi-select is not bound to an Array or Set.
            Debug.WriteLine("[Vue warn] <select multiple v-model> expects an Array or Set value for its binding.");
            return;
        }
        foreach (var (optionHandle, rawValue) in options)
        {
            bool selected;
            if (multiple)
            {
                selected = isArrayValue
                    ? LooseEquality.LooseIndexOf((IList)value!, rawValue) > -1
                    : BrowserModelDirective.SetContains((IEnumerable)value!, rawValue);
            }
            else
            {
                selected = LooseEquality.LooseEqual(rawValue, value);
            }
            operations.SetBooleanProperty(optionHandle, "selected", selected);
        }
    }

    // Map a dispatched selection string back to its raw (possibly object) option value via the
    // snapshot, applying .number coercion (upstream getValue + optional looseToNumber).
    private static object? MapSelected(List<KeyValuePair<string, object?>>? optionValues, string domValue, bool castToNumber)
    {
        object? raw = domValue;
        if (optionValues is not null)
        {
            foreach (var pair in optionValues)
            {
                if (string.Equals(pair.Key, domValue, StringComparison.Ordinal))
                {
                    raw = pair.Value;
                    break;
                }
            }
        }
        return castToNumber ? NumberCoercion.LooseToNumber(raw) : raw;
    }

    // Collect option vnodes (with their handles) in document order, recursing through optgroups and
    // v-for fragments (upstream reads the flat el.options collection).
    private static void CollectOptions(VirtualNode? node, List<(int Handle, object? RawValue)> options)
    {
        if (node?.ArrayChildren is null)
        {
            return;
        }
        foreach (var child in node.ArrayChildren)
        {
            if (child is null)
            {
                continue;
            }
            if (string.Equals(child.ElementTag, "option", StringComparison.Ordinal) && child.El is int handle)
            {
                options.Add((handle, OptionRawValue(child)));
            }
            else
            {
                CollectOptions(child, options);
            }
        }
    }

    // An option's raw value: its :value prop when present, else its text content (HTML option.value
    // defaults to option.text).
    private static object? OptionRawValue(VirtualNode option)
        => option.Properties?.ContainsName("value") == true
            ? BrowserModelDirective.Property(option, "value")
            : option.TextChildren;

    private static bool IsMultiple(VirtualNode node)
        => node.Properties?["multiple"] is bool multiple
            ? multiple
            : node.Properties?.ContainsName("multiple") == true;
}
