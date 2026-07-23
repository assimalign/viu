using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The <c>v-model</c> directive for <c>&lt;select&gt;</c> — the C# port of Vue's
/// <c>vModelSelect</c>.
/// </summary>
/// <remarks>
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-dom/src/directives/vModel.ts.
/// The directive uses <see cref="DirectiveBinding.GetDescendantElements(string)"/> to pair each
/// immutable option component with its mounted browser handle. Raw bound option values therefore
/// retain object identity without reflection or a browser read.
/// </remarks>
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

    private static void OnCreated(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        BrowserDirectiveOperations operations =
            BrowserDirectiveOperations.Require();
        int handle = BrowserModelDirective.Handle(element);
        operations.GetState(handle).Assign =
            BrowserModelDirective.Carrier(binding)?.Setter;
        bool multiple = IsMultiple(component);
        bool isSetModel =
            BrowserModelDirective.IsSet(
                BrowserModelDirective.Carrier(binding)?.Value);
        bool castToNumber =
            BrowserModelDirective.HasModifier(binding, "number");
        operations.SetModelListener(
            handle,
            "onChange",
            (Action<BrowserEvent>)(
                browserEvent =>
                    OnChange(
                        operations,
                        handle,
                        browserEvent,
                        multiple,
                        isSetModel,
                        castToNumber)));
    }

    private static void OnMounted(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        SetSelected(
            BrowserDirectiveOperations.Require(),
            BrowserModelDirective.Handle(element),
            binding,
            component,
            BrowserModelDirective.Carrier(binding)?.Value);
    }

    private static void OnBeforeUpdate(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        BrowserDirectiveOperations.Require()
            .GetState(BrowserModelDirective.Handle(element))
            .Assign = BrowserModelDirective.Carrier(binding)?.Setter;
    }

    private static void OnUpdated(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        BrowserDirectiveOperations operations =
            BrowserDirectiveOperations.Require();
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
            component,
            BrowserModelDirective.Carrier(binding)?.Value);
    }

    private static void OnBeforeUnmount(
        object element,
        DirectiveBinding binding,
        IElementComponent component,
        IElementComponent? previousComponent)
    {
        BrowserDirectiveOperations.Require()
            .ReleaseState(BrowserModelDirective.Handle(element));
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
                    selected.Add(
                        MapSelected(
                            state.OptionValues,
                            selectedValues[index],
                            castToNumber));
                }
            }

            if (isSetModel)
            {
                assign(new HashSet<object?>(selected));
            }
            else
            {
                assign(selected);
            }
        }
        else
        {
            assign(
                MapSelected(
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
        IElementComponent component,
        object? value)
    {
        IReadOnlyList<DirectiveHostElement> optionElements =
            binding.GetDescendantElements("option");
        List<KeyValuePair<string, object?>> snapshot =
            new(optionElements.Count);
        for (int index = 0; index < optionElements.Count; index++)
        {
            object? rawValue =
                OptionRawValue(optionElements[index].Component);
            snapshot.Add(
                new KeyValuePair<string, object?>(
                    BrowserModelDirective.FormatValue(rawValue),
                    rawValue));
        }

        operations.GetState(handle).OptionValues = snapshot;
        bool multiple = IsMultiple(component);
        bool isListValue = BrowserModelDirective.IsList(value);
        bool isSetValue = BrowserModelDirective.IsSet(value);
        if (multiple && !isListValue && !isSetValue)
        {
            Debug.WriteLine(
                "[Vue warn] <select multiple v-model> expects an Array or Set value for its binding.");
            return;
        }

        for (int index = 0; index < optionElements.Count; index++)
        {
            DirectiveHostElement option = optionElements[index];
            object? rawValue = snapshot[index].Value;
            bool selected =
                multiple
                    ? isListValue
                        ? LooseEquality.LooseIndexOf(
                            (IList)value!,
                            rawValue) > -1
                        : BrowserModelDirective.SetContains(
                            (IEnumerable)value!,
                            rawValue)
                    : LooseEquality.LooseEqual(rawValue, value);
            operations.SetBooleanProperty(
                BrowserModelDirective.Handle(option.Element),
                "selected",
                selected);
        }
    }

    private static object? MapSelected(
        List<KeyValuePair<string, object?>>? optionValues,
        string domValue,
        bool castToNumber)
    {
        object? rawValue = domValue;
        if (optionValues is not null)
        {
            for (int index = 0; index < optionValues.Count; index++)
            {
                KeyValuePair<string, object?> optionValue =
                    optionValues[index];
                if (string.Equals(
                    optionValue.Key,
                    domValue,
                    StringComparison.Ordinal))
                {
                    rawValue = optionValue.Value;
                    break;
                }
            }
        }

        return castToNumber
            ? NumberCoercion.LooseToNumber(rawValue)
            : rawValue;
    }

    private static object? OptionRawValue(IElementComponent option)
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
        IReadOnlyList<IComponent> components,
        StringBuilder text)
    {
        for (int index = 0; index < components.Count; index++)
        {
            switch (components[index])
            {
                case ITextComponent textComponent:
                    text.Append(textComponent.Text);
                    break;
                case IElementComponent elementComponent:
                    AppendText(elementComponent.Children, text);
                    break;
                case IFragmentComponent fragmentComponent:
                    AppendText(fragmentComponent.Children, text);
                    break;
            }
        }
    }

    private static bool IsMultiple(IElementComponent component)
    {
        if (!component.Attributes.TryGetValue(
            "multiple",
            out object? value))
        {
            return false;
        }

        return value is not bool booleanValue || booleanValue;
    }
}
