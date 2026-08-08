using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Provides shared reflection-free operations for Browser model directives.</summary>
internal static class BrowserModelDirective
{
    private static readonly string[] ModelListenerNames =
    [
        "onInput",
        "onChange",
        "onCompositionstart",
        "onCompositionend",
        "onFocus",
        "onBlur",
    ];

    public static int Handle(object element) => element is int handle
        ? handle
        : throw new InvalidOperationException(
            "A Browser model or show directive requires an integer browser-node handle.");

    public static ModelBinding? Carrier(DirectiveBinding binding) =>
        binding.Value as ModelBinding;

    public static object? ModelValue(object? binding) =>
        (binding as ModelBinding)?.Value;

    public static bool HasModifier(DirectiveBinding binding, string name)
    {
        IReadOnlyList<string>? modifiers = Carrier(binding)?.Modifiers;
        if (modifiers is null)
        {
            return false;
        }

        for (int index = 0; index < modifiers.Count; index++)
        {
            if (string.Equals(modifiers[index], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string FormatValue(object? value) =>
        value is null ? string.Empty : DisplayStringFormatter.FormatScalar(value);

    public static object? Property(ElementNode element, string name)
    {
        for (int index = element.Bindings.Count - 1; index >= 0; index--)
        {
            ElementBinding binding = element.Bindings[index];
            if (binding.Kind != ElementBindingKind.Event
                && string.Equals(binding.Name.LocalName, name, StringComparison.Ordinal))
            {
                return binding.Value;
            }
        }

        return null;
    }

    public static bool HasProperty(ElementNode element, string name)
    {
        for (int index = element.Bindings.Count - 1; index >= 0; index--)
        {
            ElementBinding binding = element.Bindings[index];
            if (binding.Kind != ElementBindingKind.Event
                && string.Equals(binding.Name.LocalName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsNumberType(ElementNode element) =>
        string.Equals(
            FormatValue(Property(element, "type")),
            "number",
            StringComparison.OrdinalIgnoreCase);

    public static bool IsList([NotNullWhen(true)] object? value) => value is IList;

    public static bool IsSet([NotNullWhen(true)] object? value) =>
        value is IEnumerable and not string and not IDictionary and not IList;

    public static List<object?> CopyToList(IEnumerable values)
    {
        List<object?> copy = [];
        foreach (object? value in values)
        {
            copy.Add(value);
        }

        return copy;
    }

    public static HashSet<object?> CopyToSet(IEnumerable values)
    {
        HashSet<object?> copy = [];
        foreach (object? value in values)
        {
            copy.Add(value);
        }

        return copy;
    }

    public static bool SetContains(IEnumerable values, object? value)
    {
        foreach (object? entry in values)
        {
            if (Equals(entry, value))
            {
                return true;
            }
        }

        return false;
    }

    public static void ClearModelListeners(
        BrowserDirectiveOperations operations,
        int handle)
    {
        for (int index = 0; index < ModelListenerNames.Length; index++)
        {
            operations.SetModelListener(handle, ModelListenerNames[index], null);
        }
    }
}
