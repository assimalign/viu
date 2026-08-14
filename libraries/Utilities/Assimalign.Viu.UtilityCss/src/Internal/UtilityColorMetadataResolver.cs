using System;
using System.Collections.Generic;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityColorMetadataResolver
{
    public static string? Resolve(
        UtilityCandidate candidate,
        UtilityTheme theme,
        IEnumerable<UtilityCssDeclaration> declarations)
    {
        var isColorBearing = false;
        foreach (var declaration in declarations)
        {
            if (IsColorProperty(declaration.Property))
            {
                isColorBearing = true;
                break;
            }
        }

        if (!isColorBearing ||
            candidate.Value is null)
        {
            return null;
        }

        string? colorValue;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (candidate.Value.DataType is not null &&
                candidate.Value.DataType != "color")
            {
                return null;
            }

            colorValue = candidate.Value.Text;
        }
        else
        {
            colorValue = candidate.Value.Text switch
            {
                "current" => "currentcolor",
                "inherit" => "inherit",
                "transparent" => "transparent",
                _ => null,
            };
            if (colorValue is null &&
                !theme.TryGetNamespaceRawValue(
                    UtilityThemeNamespaceNames.Color,
                    candidate.Value.Text,
                    out colorValue))
            {
                return null;
            }
        }

        return UtilityResolutionEngine.TryApplyColorModifier(
            colorValue!,
            candidate.Modifier,
            out var modifiedColor)
                ? modifiedColor
                : null;
    }

    public static string? ResolveProjectBody(
        string body,
        UtilityTheme theme)
    {
        string? resolvedColorValue = null;
        var segmentStart = 0;
        var propertySeparator = -1;
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var quote = '\0';
        var isEscaped = false;
        for (var index = 0; index <= body.Length; index++)
        {
            var character = index < body.Length
                ? body[index]
                : ';';
            if (quote != '\0')
            {
                if (isEscaped)
                {
                    isEscaped = false;
                }
                else if (character == '\\')
                {
                    isEscaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            switch (character)
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    break;
                case '{' when parenthesisDepth == 0 && bracketDepth == 0:
                    segmentStart = index + 1;
                    propertySeparator = -1;
                    break;
                case ':' when parenthesisDepth == 0 &&
                                   bracketDepth == 0 &&
                                   propertySeparator < 0:
                    propertySeparator = index;
                    break;
                case ';' or '}' when parenthesisDepth == 0 && bracketDepth == 0:
                    if (propertySeparator > segmentStart)
                    {
                        var property = body.Substring(
                                segmentStart,
                                propertySeparator - segmentStart)
                            .Trim();
                        if (IsColorProperty(property))
                        {
                            var value = body.Substring(
                                    propertySeparator + 1,
                                    index - propertySeparator - 1)
                                .Trim();
                            var currentColorValue = ResolveProjectValue(
                                value,
                                theme);
                            if (currentColorValue is not null)
                            {
                                if (resolvedColorValue is not null &&
                                    !string.Equals(
                                        resolvedColorValue,
                                        currentColorValue,
                                        StringComparison.Ordinal))
                                {
                                    return null;
                                }

                                resolvedColorValue = currentColorValue;
                            }
                        }
                    }

                    segmentStart = index + 1;
                    propertySeparator = -1;
                    break;
            }
        }

        return resolvedColorValue;
    }

    private static string? ResolveProjectValue(
        string value,
        UtilityTheme theme)
    {
        const string ImportantMarker = "!important";
        if (value.EndsWith(
                ImportantMarker,
                StringComparison.Ordinal))
        {
            value = value.Substring(
                    0,
                    value.Length - ImportantMarker.Length)
                .TrimEnd();
        }

        if (!value.StartsWith(
                "var(--",
                StringComparison.Ordinal))
        {
            return value.Length == 0
                ? null
                : value;
        }

        var nameStart = "var(".Length;
        var nameEnd = nameStart;
        while (nameEnd < value.Length &&
               value[nameEnd] is not ')' and not ',' &&
               !char.IsWhiteSpace(value[nameEnd]))
        {
            nameEnd++;
        }

        var customPropertyName = value.Substring(
            nameStart,
            nameEnd - nameStart);
        foreach (var property in theme.Properties)
        {
            if (property.Name.StartsWith(
                    "--color-",
                    StringComparison.Ordinal) &&
                string.Equals(
                    theme.FormatCustomPropertyName(property.Name),
                    customPropertyName,
                    StringComparison.Ordinal))
            {
                return property.Value;
            }
        }

        return value;
    }

    private static bool IsColorProperty(string property) =>
        property is "color" or "fill" or "stroke" or "scrollbar-color" or
            "--tw-gradient-from" or "--tw-gradient-via" or "--tw-gradient-to" or
            "--tw-scrollbar-thumb" or "--tw-scrollbar-track" ||
        property.EndsWith(
            "-color",
            StringComparison.Ordinal);
}
