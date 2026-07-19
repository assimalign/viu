using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// The class/style/property-vs-attribute decision tree that decides how each vnode prop lands
/// on a real DOM node — the C# port of <c>@vue/runtime-dom</c>'s <c>patchProp</c> and its
/// <c>modules/</c> (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/patchProp.ts).
/// The tree runs entirely on the .NET side over injected leaf appliers, so each resolution is
/// at most one interop call and the JS shim stays dumb. Vue reads <c>key in el</c> at runtime
/// to detect IDL properties; a handle-based bridge cannot without a round-trip, so this port
/// uses curated knowledge sets for the criteria-named cases and falls back to the attribute
/// path for unknown keys — full per-tag knowledge tables arrive with [V01.01.01.03].
/// Style map keys are CSS property names (kebab-case or <c>--custom</c>); camelCase
/// normalization arrives with the [V01.01.01.02] helpers.
/// </summary>
internal static class BrowserPropertyPatcher
{
    // Native boolean IDL properties whose attribute name matches the property name, so a
    // property write is valid as-written and reflects to the attribute (upstream: the
    // `key in el` branch of shouldSetAsProp over @vue/shared's isBooleanAttr set).
    private static readonly HashSet<string> BooleanPropertyNames = new(StringComparer.Ordinal)
    {
        "async", "autofocus", "autoplay", "checked", "controls", "default", "defer",
        "disabled", "hidden", "inert", "loop", "multiple", "muted", "open", "required",
        "reversed", "selected",
    };

    // Boolean attributes whose IDL property is spelled differently (readonly vs readOnly, …) —
    // these take the attribute path with present/absent semantics (upstream isBooleanAttr).
    private static readonly HashSet<string> BooleanAttributeNames = new(StringComparer.Ordinal)
    {
        "allowfullscreen", "formnovalidate", "ismap", "itemscope", "nomodule", "novalidate",
        "playsinline", "readonly", "scoped", "seamless",
    };

    // Enumerated attributes where false must be WRITTEN as the string "false", not removed
    // (upstream: isEnumeratedAttribute — spellcheck/draggable/translate).
    private static readonly HashSet<string> EnumeratedAttributeNames = new(StringComparer.Ordinal)
    {
        "draggable", "spellcheck", "translate",
    };

    /// <summary>Lands one prop change on <paramref name="element"/> through the leaf ops.</summary>
    /// <param name="leafOperations">The platform leaf appliers.</param>
    /// <param name="element">The element handle.</param>
    /// <param name="elementTag">The element's tag (lower-case HTML tags expected).</param>
    /// <param name="propertyName">The vnode prop name.</param>
    /// <param name="previousValue">The prior value, or null on mount.</param>
    /// <param name="nextValue">The new value, or null to remove.</param>
    /// <param name="elementNamespace"><c>"svg"</c>, <c>"mathml"</c>, or null for HTML.</param>
    internal static void Patch(
        BrowserPropertyLeafOperations leafOperations,
        int element,
        string elementTag,
        string propertyName,
        object? previousValue,
        object? nextValue,
        string? elementNamespace)
    {
        if (string.Equals(propertyName, "class", StringComparison.Ordinal))
        {
            PatchClass(leafOperations, element, nextValue, elementNamespace);
        }
        else if (string.Equals(propertyName, "style", StringComparison.Ordinal))
        {
            PatchStyle(leafOperations, element, previousValue, nextValue);
        }
        else if (VirtualNodeFactory.IsEventListenerName(propertyName))
        {
            // The raw prop name flows through: the invoker registry parses the
            // Once/Capture/Passive suffixes and event name ([V01.01.04.03]).
            leafOperations.SetEventListener(element, propertyName, nextValue as Delegate);
        }
        else if (ShouldSetAsProperty(elementTag, propertyName, elementNamespace))
        {
            PatchDomProperty(leafOperations, element, propertyName, nextValue);
        }
        else
        {
            PatchAttribute(leafOperations, element, propertyName, nextValue, elementNamespace);
        }
    }

    private static void PatchClass(BrowserPropertyLeafOperations leafOperations, int element, object? nextValue, string? elementNamespace)
    {
        // Upstream class module: one className write on HTML; setAttribute on SVG/MathML;
        // null removes the attribute.
        if (nextValue is null)
        {
            leafOperations.RemoveAttribute(element, "class");
        }
        else if (elementNamespace is not null)
        {
            leafOperations.SetAttribute(element, "class", FormatValue(nextValue));
        }
        else
        {
            leafOperations.SetClassName(element, FormatValue(nextValue));
        }
    }

    private static void PatchStyle(BrowserPropertyLeafOperations leafOperations, int element, object? previousValue, object? nextValue)
    {
        // Upstream style module: string cssText fast path; map patching touches only changed
        // keys, removes stale keys, and supports --custom properties and !important.
        if (nextValue is null)
        {
            leafOperations.RemoveAttribute(element, "style");
            return;
        }
        if (nextValue is string nextText)
        {
            if (previousValue is not string previousText
                || !string.Equals(previousText, nextText, StringComparison.Ordinal))
            {
                leafOperations.SetStyleText(element, nextText);
            }
            return;
        }
        if (nextValue is IReadOnlyDictionary<string, object?> nextMap)
        {
            var previousMap = previousValue as IReadOnlyDictionary<string, object?>;
            if (previousMap is null && previousValue is not null)
            {
                // A string (or other shape) is being replaced wholesale by a map.
                leafOperations.SetStyleText(element, string.Empty);
            }
            if (previousMap is not null)
            {
                foreach (var (styleName, _) in previousMap)
                {
                    if (!nextMap.ContainsKey(styleName))
                    {
                        leafOperations.RemoveStyleProperty(element, styleName);
                    }
                }
            }
            foreach (var (styleName, styleValue) in nextMap)
            {
                object? previousStyleValue = null;
                previousMap?.TryGetValue(styleName, out previousStyleValue);
                if (Equals(previousStyleValue, styleValue))
                {
                    continue;
                }
                ApplyStyleProperty(leafOperations, element, styleName, styleValue);
            }
            return;
        }
        leafOperations.SetStyleText(element, FormatValue(nextValue));
    }

    private static void ApplyStyleProperty(BrowserPropertyLeafOperations leafOperations, int element, string styleName, object? styleValue)
    {
        if (styleValue is null)
        {
            leafOperations.RemoveStyleProperty(element, styleName);
            return;
        }
        var formatted = FormatValue(styleValue);
        // "color:red !important" -> setProperty(name, "red", "important") (upstream importantRE).
        if (formatted.EndsWith("!important", StringComparison.Ordinal))
        {
            leafOperations.SetStyleProperty(
                element,
                styleName,
                formatted[..^"!important".Length].TrimEnd(),
                true);
        }
        else
        {
            leafOperations.SetStyleProperty(element, styleName, formatted, false);
        }
    }

    private static bool ShouldSetAsProperty(string elementTag, string propertyName, string? elementNamespace)
    {
        // Upstream shouldSetAsProp, minus the runtime `key in el` probe (see class docs).
        if (elementNamespace is not null)
        {
            // SVG/MathML land as attributes except explicit content properties.
            return string.Equals(propertyName, "innerHTML", StringComparison.Ordinal)
                || string.Equals(propertyName, "textContent", StringComparison.Ordinal);
        }
        if (string.Equals(propertyName, "innerHTML", StringComparison.Ordinal)
            || string.Equals(propertyName, "textContent", StringComparison.Ordinal))
        {
            return true;
        }
        if (string.Equals(propertyName, "value", StringComparison.Ordinal))
        {
            // value is a property on form controls; PROGRESS and everything else keep the
            // attribute (upstream parity).
            return string.Equals(elementTag, "input", StringComparison.Ordinal)
                || string.Equals(elementTag, "textarea", StringComparison.Ordinal)
                || string.Equals(elementTag, "select", StringComparison.Ordinal);
        }
        if (string.Equals(propertyName, "form", StringComparison.Ordinal))
        {
            // The form IDL property is readonly — always an attribute (upstream parity).
            return false;
        }
        if (string.Equals(propertyName, "list", StringComparison.Ordinal)
            && string.Equals(elementTag, "input", StringComparison.Ordinal))
        {
            return false;
        }
        if (string.Equals(propertyName, "type", StringComparison.Ordinal)
            && string.Equals(elementTag, "textarea", StringComparison.Ordinal))
        {
            return false;
        }
        if ((string.Equals(propertyName, "width", StringComparison.Ordinal)
                || string.Equals(propertyName, "height", StringComparison.Ordinal))
            && (string.Equals(elementTag, "img", StringComparison.Ordinal)
                || string.Equals(elementTag, "video", StringComparison.Ordinal)
                || string.Equals(elementTag, "canvas", StringComparison.Ordinal)
                || string.Equals(elementTag, "embed", StringComparison.Ordinal)))
        {
            return false;
        }
        return BooleanPropertyNames.Contains(propertyName);
    }

    private static void PatchDomProperty(BrowserPropertyLeafOperations leafOperations, int element, string propertyName, object? nextValue)
    {
        if (string.Equals(propertyName, "value", StringComparison.Ordinal))
        {
            // One guarded compare-and-set; null clears (upstream: el.value = value ?? '').
            leafOperations.SetValueGuarded(element, nextValue is null ? string.Empty : FormatValue(nextValue));
            return;
        }
        if (BooleanPropertyNames.Contains(propertyName))
        {
            // Property write reflects to the attribute: false/null removes it (upstream
            // includeBooleanAttr semantics).
            leafOperations.SetBooleanProperty(element, propertyName, IsTruthy(nextValue));
            return;
        }
        leafOperations.SetStringProperty(element, propertyName, nextValue is null ? string.Empty : FormatValue(nextValue));
    }

    private static void PatchAttribute(BrowserPropertyLeafOperations leafOperations, int element, string propertyName, object? nextValue, string? elementNamespace)
    {
        // xlink: namespace on SVG (upstream attrs module).
        if (string.Equals(elementNamespace, "svg", StringComparison.Ordinal)
            && propertyName.StartsWith("xlink:", StringComparison.Ordinal))
        {
            if (nextValue is null)
            {
                leafOperations.RemoveXlinkAttribute(element, propertyName);
            }
            else
            {
                leafOperations.SetXlinkAttribute(element, propertyName, FormatValue(nextValue));
            }
            return;
        }
        if (nextValue is null)
        {
            leafOperations.RemoveAttribute(element, propertyName);
            return;
        }
        if (nextValue is bool boolValue)
        {
            if (EnumeratedAttributeNames.Contains(propertyName))
            {
                // Enumerated attributes must WRITE "false" — removal would mean "inherit"
                // (upstream parity: spellcheck/draggable/translate).
                leafOperations.SetAttribute(element, propertyName, boolValue ? "true" : "false");
            }
            else if (boolValue)
            {
                // Present boolean attributes are written as the empty string (upstream parity).
                leafOperations.SetAttribute(element, propertyName, string.Empty);
            }
            else
            {
                leafOperations.RemoveAttribute(element, propertyName);
            }
            return;
        }
        leafOperations.SetAttribute(element, propertyName, FormatValue(nextValue));
    }

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool boolValue => boolValue,
        // Any string — including "" — keeps a boolean on (upstream includeBooleanAttr:
        // `!!value || value === ''`).
        string => true,
        _ => true,
    };

    private static string FormatValue(object value) => value switch
    {
        string text => text,
        bool boolValue => boolValue ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Whether <paramref name="attributeName"/> is a present/absent boolean attribute.</summary>
    internal static bool IsBooleanAttributeName(string attributeName)
        => BooleanPropertyNames.Contains(attributeName) || BooleanAttributeNames.Contains(attributeName);
}
