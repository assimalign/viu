using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Provides Browser single- and multiple-select two-way model behavior.</summary>
/// <remarks>
/// Descendant option snapshots pair immutable raw values with mounted handles, so object values
/// round-trip and every selection write uses event payload data instead of follow-up browser
/// reads. Multiple selection preserves list-versus-set shape. Specified by <c>[SFC-CG-6]</c> and
/// <c>[SFC-CG-7]</c>.
/// </remarks>
public sealed class VModelSelect : IDirective
{
    /// <summary>Gets the reusable Browser select-model directive.</summary>
    public static VModelSelect Instance { get; } = new();

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

    private static void OnCreated(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        BrowserDirectiveOperations operations = BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        operations.GetState(handle).Assign = BrowserModelDirective.Carrier(binding)?.Setter;
        ConfigureListener(operations, handle, binding, value);
    }

    private static void OnMounted(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        SetSelected(
            BrowserDirectiveOperations.Require(),
            BrowserModelDirective.Handle(element),
            binding,
            value,
            BrowserModelDirective.Carrier(binding)?.Value);
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
        ConfigureListener(operations, handle, binding, value);
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
        if (state.Assigning)
        {
            state.Assigning = false;
            return;
        }

        SetSelected(
            operations,
            handle,
            binding,
            value,
            BrowserModelDirective.Carrier(binding)?.Value);
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

    private static void ConfigureListener(
        BrowserDirectiveOperations operations,
        int handle,
        DirectiveBinding binding,
        ElementNode value)
    {
        bool multiple = IsMultiple(value);
        bool isSetModel = BrowserModelDirective.IsSet(
            BrowserModelDirective.Carrier(binding)?.Value);
        bool castToNumber = BrowserModelDirective.HasModifier(binding, "number");
        operations.SetModelListener(
            handle,
            "onChange",
            (Action<BrowserEvent>)(browserEvent =>
                OnChange(
                    operations,
                    handle,
                    browserEvent,
                    multiple,
                    isSetModel,
                    castToNumber)));
    }

    private static void OnChange(
        BrowserDirectiveOperations operations,
        int handle,
        BrowserEvent browserEvent,
        bool multiple,
        bool isSetModel,
        bool castToNumber)
    {
        BrowserModelState state = operations.GetState(handle);
        Action<object?>? assign = state.Assign;
        if (assign is null)
        {
            return;
        }

        if (multiple)
        {
            List<object?> selected = [];
            if (browserEvent.SelectedValues is { } selectedValues)
            {
                for (int index = 0; index < selectedValues.Count; index++)
                {
                    selected.Add(MapSelected(
                        state.OptionValues,
                        selectedValues[index],
                        castToNumber));
                }
            }

            assign(isSetModel ? new HashSet<object?>(selected) : selected);
        }
        else
        {
            assign(MapSelected(
                state.OptionValues,
                browserEvent.TargetValue ?? string.Empty,
                castToNumber));
        }

        state.Assigning = true;
    }

    private static void SetSelected(
        BrowserDirectiveOperations operations,
        int handle,
        DirectiveBinding binding,
        ElementNode value,
        object? model)
    {
        IReadOnlyList<DirectiveHostElement> options =
            binding.GetDescendantElements("option");
        List<KeyValuePair<string, object?>> snapshot = new(options.Count);
        for (int index = 0; index < options.Count; index++)
        {
            object? rawValue = OptionRawValue(options[index].Value);
            snapshot.Add(new KeyValuePair<string, object?>(
                BrowserModelDirective.FormatValue(rawValue),
                rawValue));
        }

        operations.GetState(handle).OptionValues = snapshot;
        bool multiple = IsMultiple(value);
        bool isListModel = BrowserModelDirective.IsList(model);
        bool isSetModel = BrowserModelDirective.IsSet(model);
        if (multiple && !isListModel && !isSetModel)
        {
            binding.Context?.Warn(
                "<select multiple v-model> expects a list or set model value.");
            return;
        }

        for (int index = 0; index < options.Count; index++)
        {
            object? rawValue = snapshot[index].Value;
            bool selected = multiple
                ? isListModel
                    ? LooseEquality.LooseIndexOf((IList)model!, rawValue) >= 0
                    : BrowserModelDirective.SetContains((IEnumerable)model!, rawValue)
                : LooseEquality.LooseEqual(rawValue, model);
            operations.SetBooleanProperty(
                BrowserModelDirective.Handle(options[index].Element),
                "selected",
                selected);
        }
    }

    private static object? MapSelected(
        List<KeyValuePair<string, object?>>? optionValues,
        string browserValue,
        bool castToNumber)
    {
        object? rawValue = browserValue;
        if (optionValues is not null)
        {
            for (int index = 0; index < optionValues.Count; index++)
            {
                if (string.Equals(
                    optionValues[index].Key,
                    browserValue,
                    StringComparison.Ordinal))
                {
                    rawValue = optionValues[index].Value;
                    break;
                }
            }
        }

        return castToNumber ? NumberCoercion.LooseToNumber(rawValue) : rawValue;
    }

    private static object? OptionRawValue(ElementNode option)
    {
        if (BrowserModelDirective.HasProperty(option, "value"))
        {
            return BrowserModelDirective.Property(option, "value");
        }

        StringBuilder text = new();
        AppendText(option.Children, text);
        return text.ToString();
    }

    private static void AppendText(
        IReadOnlyList<VirtualNode> nodes,
        StringBuilder text)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            switch (nodes[index])
            {
                case TextNode textNode:
                    text.Append(textNode.Text);
                    break;
                case ElementNode elementNode:
                    AppendText(elementNode.Children, text);
                    break;
                case FragmentNode fragmentNode:
                    AppendText(fragmentNode.Children, text);
                    break;
            }
        }
    }

    private static bool IsMultiple(ElementNode value)
    {
        if (!BrowserModelDirective.HasProperty(value, "multiple"))
        {
            return false;
        }

        return BrowserModelDirective.Property(value, "multiple") is not bool booleanValue
            || booleanValue;
    }
}
