using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityGradientMaskResolution
{
    private const string GradientStops =
        "var(--tw-gradient-via-stops, var(--tw-gradient-position), var(--tw-gradient-from) var(--tw-gradient-from-position), var(--tw-gradient-to) var(--tw-gradient-to-position))";
    private const string MaskImage =
        "var(--tw-mask-linear), var(--tw-mask-radial), var(--tw-mask-conic)";
    private const string MaskLinearLayers =
        "var(--tw-mask-left), var(--tw-mask-right), var(--tw-mask-bottom), var(--tw-mask-top)";
    private const string TransformFunctions =
        "var(--tw-rotate-x,) var(--tw-rotate-y,) var(--tw-rotate-z,) var(--tw-skew-x,) var(--tw-skew-y,)";

    public static bool IsGradientStopRoot(string root) =>
        root is "from" or "via" or "to";

    public static bool IsMaskStopRoot(string root) =>
        root is
            "mask-x-from" or
            "mask-x-to" or
            "mask-y-from" or
            "mask-y-to" or
            "mask-t-from" or
            "mask-t-to" or
            "mask-r-from" or
            "mask-r-to" or
            "mask-b-from" or
            "mask-b-to" or
            "mask-l-from" or
            "mask-l-to" or
            "mask-linear-from" or
            "mask-linear-to" or
            "mask-radial-from" or
            "mask-radial-to" or
            "mask-conic-from" or
            "mask-conic-to";

    public static bool IsDepthTransformRoot(string root) =>
        root is "translate-z" or "rotate-z" or "scale-z";

    public static bool TryResolveArbitraryBackground(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.Root != "bg" ||
            candidate.Value?.Kind is not (UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable) ||
            !IsSafeValue(candidate.Value.Text))
        {
            return false;
        }

        var type = candidate.Value.DataType ??
                   InferBackgroundDataType(candidate.Value.Text);
        switch (type)
        {
            case "percentage":
            case "position":
                return TryAddUnmodifiedDeclaration(
                    candidate,
                    declarations,
                    "background-position");
            case "bg-size":
            case "length":
            case "size":
                return TryAddUnmodifiedDeclaration(
                    candidate,
                    declarations,
                    "background-size");
            case "image":
            case "url":
                return TryAddUnmodifiedDeclaration(
                    candidate,
                    declarations,
                    "background-image");
            case "color":
            case null:
                if (!TryApplyColorModifier(
                        candidate.Value.Text,
                        candidate.Modifier,
                        out var color))
                {
                    return false;
                }

                declarations.Add(
                    new UtilityCssDeclaration(
                        "background-color",
                        color!));
                return true;
            default:
                return false;
        }
    }

    public static bool TryResolveBackgroundGradient(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.Root is not ("bg-linear" or "bg-radial" or "bg-conic"))
        {
            return false;
        }

        if (candidate.Value?.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (candidate.Modifier is not null ||
                !IsSafeValue(candidate.Value.Text))
            {
                return false;
            }

            var functionName = candidate.Root switch
            {
                "bg-linear" => "linear-gradient",
                "bg-radial" => "radial-gradient",
                _ => "conic-gradient",
            };
            var value = candidate.IsNegative
                ? "calc(" + candidate.Value.Text + " * -1)"
                : candidate.Value.Text;
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-gradient-position",
                    value));
            declarations.Add(
                new UtilityCssDeclaration(
                    "background-image",
                    functionName + "(var(--tw-gradient-stops," + value + "))"));
            return true;
        }

        if (!TryResolveInterpolationMethod(
                candidate.Modifier,
                out var interpolationMethod))
        {
            return false;
        }

        string? gradientPosition;
        string function;
        switch (candidate.Root)
        {
            case "bg-linear":
                if (candidate.Value is null)
                {
                    return false;
                }

                gradientPosition =
                    TryResolveLinearGradientPosition(candidate);
                function = "linear-gradient";
                break;
            case "bg-conic":
                if (candidate.Value is null)
                {
                    gradientPosition = interpolationMethod;
                }
                else
                {
                    var angle = TryResolveNamedAngle(
                        candidate.Value.Text,
                        candidate.IsNegative);
                    gradientPosition = angle is null
                        ? null
                        : "from " + angle + " " + interpolationMethod;
                }

                function = "conic-gradient";
                break;
            default:
                if (candidate.Value is not null ||
                    candidate.IsNegative)
                {
                    return false;
                }

                gradientPosition = interpolationMethod;
                function = "radial-gradient";
                break;
        }

        if (gradientPosition is null)
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-gradient-position",
                gradientPosition));
        declarations.Add(
            new UtilityCssDeclaration(
                "background-image",
                function + "(var(--tw-gradient-stops))"));
        return true;
    }

    public static bool TryGetLinearGradientInterpolationOverride(
        UtilityCandidate candidate,
        out string? position)
    {
        position = null;
        if (candidate.Root != "bg-linear" ||
            candidate.Value?.Kind != UtilityValueKind.Named ||
            !TryResolveInterpolationMethod(
                candidate.Modifier,
                out var interpolationMethod))
        {
            return false;
        }

        var fallbackPosition =
            TryResolveLinearGradientPosition(candidate);
        if (fallbackPosition is null)
        {
            return false;
        }

        position =
            fallbackPosition + " " + interpolationMethod;
        return true;
    }

    public static bool TryResolveGradientStop(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (!IsGradientStopRoot(candidate.Root) ||
            candidate.IsNegative ||
            !TryResolveGradientStopValue(
                candidate,
                theme,
                out var value,
                out var isColor))
        {
            return false;
        }

        if (!isColor)
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-gradient-" + candidate.Root + "-position",
                    value!));
            return true;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-gradient-" + candidate.Root,
                value!));
        if (candidate.Root == "via")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-gradient-via-stops",
                    "var(--tw-gradient-position), var(--tw-gradient-from) var(--tw-gradient-from-position), var(--tw-gradient-via) var(--tw-gradient-via-position), var(--tw-gradient-to) var(--tw-gradient-to-position)"));
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-gradient-stops",
                    "var(--tw-gradient-via-stops)"));
        }
        else
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-gradient-stops",
                    GradientStops));
        }

        return true;
    }

    public static bool TryResolveMaskStop(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (!IsMaskStopRoot(candidate.Root) ||
            candidate.IsNegative ||
            !TryResolveMaskStopValue(
                candidate,
                theme,
                out var value,
                out var isColor))
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "mask-image",
                MaskImage));
        declarations.Add(
            new UtilityCssDeclaration(
                "mask-composite",
                "intersect"));

        if (TryGetMaskEdges(
                candidate.Root,
                out var edges,
                out var stop))
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "--tw-mask-linear",
                    MaskLinearLayers));
            foreach (var edge in edges!)
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-mask-" + edge,
                        CreateEdgeMask(edge)));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-mask-" + edge + "-" + stop + "-" +
                        (isColor
                            ? "color"
                            : "position"),
                        value!));
            }

            return true;
        }

        var geometry = candidate.Root.StartsWith(
            "mask-linear-",
            StringComparison.Ordinal)
                ? "linear"
                : candidate.Root.StartsWith(
                    "mask-radial-",
                    StringComparison.Ordinal)
                    ? "radial"
                    : "conic";
        stop = candidate.Root.EndsWith(
            "-from",
            StringComparison.Ordinal)
                ? "from"
                : "to";
        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-mask-" + geometry + "-stops",
                CreateMaskStops(geometry)));
        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-mask-" + geometry,
                geometry + "-gradient(var(--tw-mask-" + geometry + "-stops))"));
        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-mask-" + geometry + "-" + stop + "-" +
                (isColor
                    ? "color"
                    : "position"),
                value!));
        return true;
    }

    public static bool TryResolveDepthTransform(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (!IsDepthTransformRoot(candidate.Root) ||
            candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        switch (candidate.Root)
        {
            case "translate-z":
                if (!TryResolveDepthTranslation(
                        candidate,
                        theme,
                        out var translation))
                {
                    return false;
                }

                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-translate-z",
                        translation!));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "translate",
                        "var(--tw-translate-x) var(--tw-translate-y) var(--tw-translate-z)"));
                return true;
            case "rotate-z":
                if (!TryResolveDepthRotation(
                        candidate,
                        out var rotation))
                {
                    return false;
                }

                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-rotate-z",
                        "rotateZ(" + rotation + ")"));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "transform",
                        TransformFunctions));
                return true;
            default:
                if (!TryResolveDepthScale(
                        candidate,
                        out var scale))
                {
                    return false;
                }

                declarations.Add(
                    new UtilityCssDeclaration(
                        "--tw-scale-z",
                        scale!));
                declarations.Add(
                    new UtilityCssDeclaration(
                        "scale",
                        "var(--tw-scale-x) var(--tw-scale-y) var(--tw-scale-z)"));
                return true;
        }
    }

    private static bool TryResolveGradientStopValue(
        UtilityCandidate candidate,
        UtilityTheme theme,
        out string? value,
        out bool isColor)
    {
        value = null;
        isColor = false;
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeValue(candidate.Value.Text))
            {
                return false;
            }

            if (candidate.Value.DataType is "length" or "percentage" ||
                candidate.Value.DataType is null &&
                candidate.Value.Kind == UtilityValueKind.Arbitrary &&
                LooksLikeLengthPercentage(candidate.Value.Text))
            {
                if (candidate.Modifier is not null)
                {
                    return false;
                }

                value = candidate.Value.Text;
                return true;
            }

            if (candidate.Value.DataType is not null and not "color")
            {
                return false;
            }

            isColor = true;
            return TryApplyColorModifier(
                candidate.Value.Text,
                candidate.Modifier,
                out value);
        }

        if (TryResolveNamedColor(
                candidate.Value.Text,
                theme,
                out var color))
        {
            isColor = true;
            return TryApplyColorModifier(
                color!,
                candidate.Modifier,
                out value);
        }

        if (candidate.Modifier is not null)
        {
            return false;
        }

        if (TryResolveGradientPosition(
                candidate.Value.Text,
                theme,
                out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveMaskStopValue(
        UtilityCandidate candidate,
        UtilityTheme theme,
        out string? value,
        out bool isColor)
    {
        value = null;
        isColor = false;
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeValue(candidate.Value.Text))
            {
                return false;
            }

            var dataType = candidate.Value.DataType;
            isColor =
                dataType == "color" ||
                dataType is null &&
                candidate.Value.Kind == UtilityValueKind.Arbitrary &&
                LooksLikeColor(candidate.Value.Text);
            if (dataType is not null and not ("color" or "length" or "percentage"))
            {
                return false;
            }

            if (isColor)
            {
                return TryApplyColorModifier(
                    candidate.Value.Text,
                    candidate.Modifier,
                    out value);
            }

            if (candidate.Modifier is not null)
            {
                return false;
            }

            if (dataType == "percentage" ||
                dataType is null &&
                candidate.Value.Text.EndsWith(
                    "%",
                    StringComparison.Ordinal))
            {
                return TryResolvePercentage(
                    candidate.Value.Text,
                    out value);
            }

            value = candidate.Value.Text;
            return true;
        }

        if (TryResolveNamedColor(
                candidate.Value.Text,
                theme,
                out var color))
        {
            isColor = true;
            return TryApplyColorModifier(
                color!,
                candidate.Modifier,
                out value);
        }

        if (candidate.Modifier is not null)
        {
            return false;
        }

        if (TryResolvePercentage(
                candidate.Value.Text,
                out value))
        {
            return true;
        }

        if (theme.TryGetNamespaceValue(
                UtilityThemeNamespaceNames.Spacing,
                candidate.Value.Text,
                out value))
        {
            return true;
        }

        return IsCanonicalSpacingMultiplier(candidate.Value.Text) &&
            theme.TryGetSpacing(
                candidate.Value.Text,
                out value);
    }

    private static bool TryResolveNamedColor(
        string name,
        UtilityTheme theme,
        out string? value)
    {
        value = name switch
        {
            "current" => "currentcolor",
            "inherit" => "inherit",
            "transparent" => "transparent",
            _ => null,
        };
        return value is not null ||
               theme.TryGetColor(name, out value);
    }

    private static bool TryApplyColorModifier(
        string color,
        UtilityModifier? modifier,
        out string? value)
    {
        if (modifier is null)
        {
            value = color;
            return true;
        }

        if (!TryResolveOpacityModifier(
                modifier,
                out var opacity))
        {
            value = null;
            return false;
        }

        value =
            "color-mix(in oklab, " + color + " " + opacity + ", transparent)";
        return true;
    }

    private static bool TryResolveOpacityModifier(
        UtilityModifier modifier,
        out string? opacity)
    {
        if (modifier.Kind == UtilityModifierKind.Named)
        {
            if (!decimal.TryParse(
                    modifier.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var number) ||
                number < decimal.Zero ||
                number > 100m)
            {
                opacity = null;
                return false;
            }

            opacity =
                number.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) + "%";
            return true;
        }

        if (!IsSafeValue(modifier.Text))
        {
            opacity = null;
            return false;
        }

        if (modifier.Text.EndsWith(
                "%",
                StringComparison.Ordinal) ||
            modifier.Kind == UtilityModifierKind.CssVariable)
        {
            opacity = modifier.Text;
            return true;
        }

        opacity = "calc(" + modifier.Text + " * 100%)";
        return true;
    }

    private static bool TryResolveGradientPosition(
        string name,
        UtilityTheme theme,
        out string? value)
    {
        if (TryResolvePercentage(
                name,
                out value))
        {
            return true;
        }

        if (!theme.TryGetProperty(
                "--gradient-color-stop-positions-" + name,
                out var property) ||
            property is null)
        {
            value = null;
            return false;
        }

        if ((property.Options & UtilityThemeOptions.Inline) != 0)
        {
            value = property.Value;
            return true;
        }

        var propertyName =
            theme.FormatCustomPropertyName(property.Name);
        value = (property.Options & UtilityThemeOptions.Reference) != 0
            ? "var(" + propertyName + ", " + property.Value + ")"
            : "var(" + propertyName + ")";
        return true;
    }

    private static bool TryResolvePercentage(
        string text,
        out string? value)
    {
        value = null;
        if (!text.EndsWith(
                "%",
                StringComparison.Ordinal) ||
            !int.TryParse(
                text.Substring(0, text.Length - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var percentage) ||
            percentage < 0)
        {
            return false;
        }

        value =
            percentage.ToString(
                CultureInfo.InvariantCulture) + "%";
        return true;
    }

    private static bool IsCanonicalSpacingMultiplier(string value) =>
        decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var number) &&
        number >= decimal.Zero &&
        number % 0.25m == decimal.Zero &&
        string.Equals(
            value,
            number.ToString(
                "0.############################",
                CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    private static string? InferBackgroundDataType(string value)
    {
        if (value.StartsWith(
                "url(",
                StringComparison.OrdinalIgnoreCase) ||
            value.IndexOf(
                "gradient(",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.StartsWith(
                "image(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "image-set(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "cross-fade(",
                StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (value.EndsWith(
                "%",
                StringComparison.Ordinal) ||
            LooksLikePosition(value))
        {
            return "position";
        }

        if (value is "auto" or "cover" or "contain")
        {
            return "bg-size";
        }

        return "color";
    }

    private static bool LooksLikePosition(string value)
    {
        if (value is
            "top" or
            "right" or
            "bottom" or
            "left" or
            "center")
        {
            return true;
        }

        if (value.IndexOf(
                ' ') >= 0)
        {
            var components = value.Split(' ');
            foreach (var component in components)
            {
                if (component.Length == 0)
                {
                    continue;
                }

                if (component is
                        "top" or
                        "right" or
                        "bottom" or
                        "left" or
                        "center" ||
                    LooksLikeLengthPercentage(component))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        return LooksLikeLengthPercentage(value);
    }

    private static bool LooksLikeLengthPercentage(string value)
    {
        if (value.StartsWith(
                "calc(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "min(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "max(",
                StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(
                "clamp(",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var suffix in new[]
                 {
                     "%",
                     "cap",
                     "ch",
                     "cm",
                     "dvh",
                     "dvw",
                     "em",
                     "ex",
                     "ic",
                     "in",
                     "lh",
                     "lvh",
                     "lvw",
                     "mm",
                     "pc",
                     "pt",
                     "px",
                     "q",
                     "rcap",
                     "rch",
                     "rem",
                     "rex",
                     "ric",
                     "rlh",
                     "svh",
                     "svw",
                     "vb",
                     "vh",
                     "vi",
                     "vmax",
                     "vmin",
                     "vw",
                 })
        {
            if (value.EndsWith(
                    suffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return value == "0";
    }

    private static bool LooksLikeColor(string value)
    {
        if (value.StartsWith(
                "#",
                StringComparison.Ordinal) ||
            value is "currentcolor" or "inherit" or "transparent")
        {
            return true;
        }

        foreach (var function in new[]
                 {
                     "color(",
                     "color-mix(",
                     "hsl(",
                     "hsla(",
                     "lab(",
                     "lch(",
                     "light-dark(",
                     "oklab(",
                     "oklch(",
                     "rgb(",
                     "rgba(",
                 })
        {
            if (value.StartsWith(
                    function,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (value.Length == 0 ||
            !char.IsLetter(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsLetter(character) &&
                character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAddUnmodifiedDeclaration(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations,
        string property)
    {
        if (candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                property,
                candidate.Value.Text));
        return true;
    }

    private static bool TryResolveInterpolationMethod(
        UtilityModifier? modifier,
        out string? value)
    {
        value = "in oklab";
        if (modifier is null)
        {
            return true;
        }

        if (!IsSafeValue(modifier.Text))
        {
            value = null;
            return false;
        }

        if (modifier.Kind is UtilityModifierKind.Arbitrary or UtilityModifierKind.CssVariable)
        {
            value = modifier.Text;
            return true;
        }

        value = modifier.Text switch
        {
            "longer" or "shorter" or "increasing" or "decreasing" =>
                "in oklch " + modifier.Text + " hue",
            "hsl" or "oklab" or "oklch" or "srgb" =>
                "in " + modifier.Text,
            _ => null,
        };
        return value is not null;
    }

    private static string? TryResolveLinearGradientPosition(
        UtilityCandidate candidate) =>
        candidate.Value?.Text switch
        {
            "to-t" => "to top",
            "to-tr" => "to top right",
            "to-r" => "to right",
            "to-br" => "to bottom right",
            "to-b" => "to bottom",
            "to-bl" => "to bottom left",
            "to-l" => "to left",
            "to-tl" => "to top left",
            null => null,
            _ => TryResolveNamedAngle(
                candidate.Value.Text,
                candidate.IsNegative),
        };

    private static string? TryResolveNamedAngle(
        string text,
        bool isNegative)
    {
        if (!int.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var degrees) ||
            degrees < 0)
        {
            return null;
        }

        var value =
            degrees.ToString(
                CultureInfo.InvariantCulture) + "deg";
        return isNegative
            ? "calc(" + value + " * -1)"
            : value;
    }

    private static bool TryGetMaskEdges(
        string root,
        out string[]? edges,
        out string? stop)
    {
        stop = root.EndsWith(
            "-from",
            StringComparison.Ordinal)
                ? "from"
                : root.EndsWith(
                    "-to",
                    StringComparison.Ordinal)
                    ? "to"
                    : null;
        edges = root switch
        {
            "mask-x-from" or "mask-x-to" => new[] { "right", "left" },
            "mask-y-from" or "mask-y-to" => new[] { "top", "bottom" },
            "mask-t-from" or "mask-t-to" => new[] { "top" },
            "mask-r-from" or "mask-r-to" => new[] { "right" },
            "mask-b-from" or "mask-b-to" => new[] { "bottom" },
            "mask-l-from" or "mask-l-to" => new[] { "left" },
            _ => null,
        };
        return edges is not null &&
               stop is not null;
    }

    private static string CreateEdgeMask(string edge) =>
        "linear-gradient(to " + edge +
        ", var(--tw-mask-" + edge + "-from-color) var(--tw-mask-" + edge +
        "-from-position), var(--tw-mask-" + edge + "-to-color) var(--tw-mask-" +
        edge + "-to-position))";

    private static string CreateMaskStops(string geometry) =>
        geometry switch
        {
            "linear" =>
                "var(--tw-mask-linear-position), var(--tw-mask-linear-from-color) var(--tw-mask-linear-from-position), var(--tw-mask-linear-to-color) var(--tw-mask-linear-to-position)",
            "radial" =>
                "var(--tw-mask-radial-shape) var(--tw-mask-radial-size) at var(--tw-mask-radial-position), var(--tw-mask-radial-from-color) var(--tw-mask-radial-from-position), var(--tw-mask-radial-to-color) var(--tw-mask-radial-to-position)",
            _ =>
                "from var(--tw-mask-conic-position), var(--tw-mask-conic-from-color) var(--tw-mask-conic-from-position), var(--tw-mask-conic-to-color) var(--tw-mask-conic-to-position)",
        };

    private static bool TryResolveDepthTranslation(
        UtilityCandidate candidate,
        UtilityTheme theme,
        out string? value)
    {
        value = null;
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeValue(candidate.Value.Text))
            {
                return false;
            }

            value = candidate.Value.Text;
        }
        else if (candidate.Value.Text == "px")
        {
            value = "1px";
        }
        else if (!theme.TryGetSpacing(
                     candidate.Value.Text,
                     out value))
        {
            return false;
        }

        if (candidate.IsNegative)
        {
            value = "calc(" + value + " * -1)";
        }

        return true;
    }

    private static bool TryResolveDepthRotation(
        UtilityCandidate candidate,
        out string? value)
    {
        value = null;
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeValue(candidate.Value.Text))
            {
                return false;
            }

            value = candidate.Value.Text;
        }
        else
        {
            value = TryResolveNamedAngle(
                candidate.Value.Text,
                false);
        }

        if (value is not null &&
            candidate.IsNegative)
        {
            value = "calc(" + value + " * -1)";
        }

        return value is not null;
    }

    private static bool TryResolveDepthScale(
        UtilityCandidate candidate,
        out string? value)
    {
        value = null;
        if (candidate.Value is null)
        {
            return false;
        }

        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafeValue(candidate.Value.Text))
            {
                return false;
            }

            value = candidate.Value.Text;
        }
        else if (int.TryParse(
                     candidate.Value.Text,
                     NumberStyles.None,
                     CultureInfo.InvariantCulture,
                     out var percentage) &&
                 percentage >= 0)
        {
            value =
                percentage.ToString(
                    CultureInfo.InvariantCulture) + "%";
        }

        if (value is not null &&
            candidate.IsNegative)
        {
            value = "calc(" + value + " * -1)";
        }

        return value is not null;
    }

    private static bool IsSafeValue(string value) =>
        !string.IsNullOrEmpty(value) &&
        UtilityCssText.IsSafeValue(value);
}
