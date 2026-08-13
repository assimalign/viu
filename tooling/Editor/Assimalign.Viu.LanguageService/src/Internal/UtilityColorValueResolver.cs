using System;
using System.Collections.Generic;

using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Resolves the color token used by emitted utility CSS back to the active theme's computed value,
/// keeping editor swatches on the same registry/theme path as build-time utility generation.
/// [V01.01.12.07.13]
/// </summary>
internal sealed class UtilityColorValueResolver
{
    private readonly IReadOnlyDictionary<string, string> valuesByCustomProperty;
    private readonly IReadOnlyList<string> inlineValues;

    private UtilityColorValueResolver(
        IReadOnlyDictionary<string, string> valuesByCustomProperty,
        IReadOnlyList<string> inlineValues)
    {
        this.valuesByCustomProperty = valuesByCustomProperty;
        this.inlineValues = inlineValues;
    }

    /// <summary>Builds a resolver from every active <c>--color-*</c> theme property.</summary>
    internal static UtilityColorValueResolver Create(UtilityTheme theme)
    {
        var valuesByCustomProperty = new Dictionary<string, string>(StringComparer.Ordinal);
        var inlineValues = new List<string>();
        foreach (var property in theme.Properties)
        {
            if (!property.Name.StartsWith("--color-", StringComparison.Ordinal))
            {
                continue;
            }

            valuesByCustomProperty[theme.FormatCustomPropertyName(property.Name)] = property.Value;
            if (!inlineValues.Contains(property.Value))
            {
                inlineValues.Add(property.Value);
            }
        }

        return new UtilityColorValueResolver(valuesByCustomProperty, inlineValues);
    }

    /// <summary>
    /// Returns the active theme value used by <paramref name="metadata"/>, including inline-theme
    /// emission where the CSS carries the value directly instead of a custom-property reference.
    /// </summary>
    internal string? Resolve(UtilityClassMetadata metadata)
    {
        var searchStart = 0;
        while (searchStart < metadata.Css.Length)
        {
            var variableStart = metadata.Css.IndexOf("var(--", searchStart, StringComparison.Ordinal);
            if (variableStart < 0)
            {
                break;
            }

            var nameStart = variableStart + "var(".Length;
            var nameEnd = nameStart;
            while (nameEnd < metadata.Css.Length &&
                   metadata.Css[nameEnd] is not ')' and not ',' &&
                   !char.IsWhiteSpace(metadata.Css[nameEnd]))
            {
                nameEnd++;
            }

            var customPropertyName = metadata.Css.Substring(nameStart, nameEnd - nameStart);
            if (valuesByCustomProperty.TryGetValue(customPropertyName, out var value))
            {
                return value;
            }

            searchStart = nameEnd;
        }

        foreach (var inlineValue in inlineValues)
        {
            if (metadata.Css.Contains(inlineValue, StringComparison.Ordinal))
            {
                return inlineValue;
            }
        }

        return null;
    }
}
