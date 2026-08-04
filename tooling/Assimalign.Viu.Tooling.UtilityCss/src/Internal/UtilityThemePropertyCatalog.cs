using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityThemePropertyCatalog
{
    private static readonly NamespaceDefinition[] Definitions =
    {
        new(
            UtilityThemeNamespace.FontWeight,
            UtilityThemeNamespaceNames.FontWeight,
            "--font-weight-"),
        new(
            UtilityThemeNamespace.InsetShadow,
            UtilityThemeNamespaceNames.InsetShadow,
            "--inset-shadow-"),
        new(
            UtilityThemeNamespace.DropShadow,
            UtilityThemeNamespaceNames.DropShadow,
            "--drop-shadow-"),
        new(
            UtilityThemeNamespace.TextShadow,
            UtilityThemeNamespaceNames.TextShadow,
            "--text-shadow-"),
        new(
            UtilityThemeNamespace.LetterSpacing,
            UtilityThemeNamespaceNames.LetterSpacing,
            "--tracking-"),
        new(
            UtilityThemeNamespace.LineHeight,
            UtilityThemeNamespaceNames.LineHeight,
            "--leading-"),
        new(
            UtilityThemeNamespace.TabSize,
            UtilityThemeNamespaceNames.TabSize,
            "--tab-size-"),
        new(
            UtilityThemeNamespace.Breakpoint,
            UtilityThemeNamespaceNames.Breakpoint,
            "--breakpoint-"),
        new(
            UtilityThemeNamespace.Container,
            UtilityThemeNamespaceNames.Container,
            "--container-"),
        new(
            UtilityThemeNamespace.Spacing,
            UtilityThemeNamespaceNames.Spacing,
            "--spacing-"),
        new(
            UtilityThemeNamespace.Color,
            UtilityThemeNamespaceNames.Color,
            "--color-"),
        new(
            UtilityThemeNamespace.FontSize,
            UtilityThemeNamespaceNames.FontSize,
            "--text-"),
        new(
            UtilityThemeNamespace.FontFamily,
            UtilityThemeNamespaceNames.FontFamily,
            "--font-"),
        new(
            UtilityThemeNamespace.Radius,
            UtilityThemeNamespaceNames.Radius,
            "--radius-"),
        new(
            UtilityThemeNamespace.Shadow,
            UtilityThemeNamespaceNames.Shadow,
            "--shadow-"),
        new(
            UtilityThemeNamespace.Blur,
            UtilityThemeNamespaceNames.Blur,
            "--blur-"),
        new(
            UtilityThemeNamespace.Perspective,
            UtilityThemeNamespaceNames.Perspective,
            "--perspective-"),
        new(
            UtilityThemeNamespace.Zoom,
            UtilityThemeNamespaceNames.Zoom,
            "--zoom-"),
        new(
            UtilityThemeNamespace.AspectRatio,
            UtilityThemeNamespaceNames.AspectRatio,
            "--aspect-"),
        new(
            UtilityThemeNamespace.TransitionTiming,
            UtilityThemeNamespaceNames.TransitionTiming,
            "--ease-"),
        new(
            UtilityThemeNamespace.Animation,
            UtilityThemeNamespaceNames.Animation,
            "--animate-"),
    };

    private static readonly Dictionary<string, NamespaceDefinition> DefinitionsByName =
        CreateDefinitionsByName();

    public static bool TryGetToken(
        string propertyName,
        out UtilityThemeNamespace themeNamespace,
        out string? tokenName)
    {
        if (TryGetExactDefault(
                propertyName,
                out themeNamespace,
                out tokenName))
        {
            return true;
        }

        foreach (var definition in Definitions)
        {
            if (TryGetSuffix(
                    propertyName,
                    definition.PropertyPrefix,
                    out tokenName) &&
                tokenName!.IndexOf(
                    "--",
                    StringComparison.Ordinal) < 0)
            {
                themeNamespace = definition.ThemeNamespace;
                return true;
            }
        }

        themeNamespace = default;
        tokenName = null;
        return false;
    }

    public static string GetName(UtilityThemeNamespace themeNamespace)
    {
        foreach (var definition in Definitions)
        {
            if (definition.ThemeNamespace == themeNamespace)
            {
                return definition.Name;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(themeNamespace),
            themeNamespace,
            "The theme namespace is not registered.");
    }

    public static bool IsKnownName(string namespaceName) =>
        DefinitionsByName.ContainsKey(namespaceName);

    public static IEnumerable<string> Names
    {
        get
        {
            foreach (var definition in Definitions)
            {
                yield return definition.Name;
            }
        }
    }

    private static bool TryGetExactDefault(
        string propertyName,
        out UtilityThemeNamespace themeNamespace,
        out string? tokenName)
    {
        switch (propertyName)
        {
            case "--blur":
                themeNamespace = UtilityThemeNamespace.Blur;
                break;
            case "--drop-shadow":
                themeNamespace = UtilityThemeNamespace.DropShadow;
                break;
            case "--inset-shadow":
                themeNamespace = UtilityThemeNamespace.InsetShadow;
                break;
            case "--radius":
                themeNamespace = UtilityThemeNamespace.Radius;
                break;
            case "--shadow":
                themeNamespace = UtilityThemeNamespace.Shadow;
                break;
            default:
                themeNamespace = default;
                tokenName = null;
                return false;
        }

        tokenName = "DEFAULT";
        return true;
    }

    private static bool TryGetSuffix(
        string propertyName,
        string prefix,
        out string? suffix)
    {
        if (propertyName.Length > prefix.Length &&
            propertyName.StartsWith(
                prefix,
                StringComparison.Ordinal))
        {
            suffix = propertyName.Substring(prefix.Length);
            return true;
        }

        suffix = null;
        return false;
    }

    private static Dictionary<string, NamespaceDefinition> CreateDefinitionsByName()
    {
        var result = new Dictionary<string, NamespaceDefinition>(
            StringComparer.Ordinal);
        foreach (var definition in Definitions)
        {
            result.Add(definition.Name, definition);
        }

        return result;
    }

    private sealed class NamespaceDefinition
    {
        public NamespaceDefinition(
            UtilityThemeNamespace themeNamespace,
            string name,
            string propertyPrefix)
        {
            ThemeNamespace = themeNamespace;
            Name = name;
            PropertyPrefix = propertyPrefix;
        }

        public UtilityThemeNamespace ThemeNamespace { get; }

        public string Name { get; }

        public string PropertyPrefix { get; }
    }
}
