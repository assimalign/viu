using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityAdditionalResolution
{
    public static bool TryResolve(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations,
        out bool isValid)
    {
        switch (candidate.Root)
        {
            case "aspect" when candidate.Value?.Fraction is not null:
                isValid = TryResolveAspect(candidate, declarations);
                return true;
            case "auto-cols":
            case "auto-rows":
                if (candidate.Value?.Kind == UtilityValueKind.Named &&
                    IsCanonicalNumber(candidate.Value.Text, out _))
                {
                    isValid = TryResolveAutomaticGridTrack(
                        candidate,
                        theme,
                        declarations);
                    return true;
                }

                break;
            case "col-span":
            case "row-span":
                isValid = TryResolveGridSpan(candidate, declarations);
                return true;
            case "contain" when candidate.Value?.Kind is
                UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable:
                isValid = TryResolveArbitraryProperty(
                    candidate,
                    "contain",
                    declarations);
                return true;
            case "decoration":
                if (candidate.Value?.Kind == UtilityValueKind.Named &&
                    IsCanonicalNonnegativeInteger(candidate.Value.Text))
                {
                    isValid = TryResolveDecorationThickness(
                        candidate,
                        declarations);
                    return true;
                }

                break;
            case "divide" when candidate.Value?.Kind == UtilityValueKind.Named &&
                                    candidate.Value.Text is
                                        "solid" or
                                        "dashed" or
                                        "dotted" or
                                        "double" or
                                        "none":
                isValid = TryResolveDivideStyle(candidate, declarations);
                return true;
            case "flex" when candidate.Value is not null:
                isValid = TryResolveFlex(candidate, declarations);
                return true;
            case "flex-auto":
            case "flex-initial":
            case "flex-none":
                isValid = TryResolveStaticFlex(candidate, declarations);
                return true;
            case "grid-cols":
            case "grid-rows":
                isValid = TryResolveGridTrack(candidate, declarations);
                return true;
            case "font-stretch":
                isValid = TryResolveFontStretch(candidate, declarations);
                return true;
            case "object" when candidate.Value?.Kind == UtilityValueKind.Named &&
                               candidate.Value.Text is
                                   "top-left" or
                                   "top-right" or
                                   "bottom-left" or
                                   "bottom-right":
                isValid = TryResolveObjectCorner(candidate, declarations);
                return true;
            case "text" when candidate.Value?.Kind is
                UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable:
                isValid = TryResolveArbitraryText(
                    candidate,
                    theme,
                    declarations);
                return true;
        }

        isValid = false;
        return false;
    }

    private static bool TryResolveAspect(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Value is null ||
            candidate.Modifier is null ||
            candidate.Value.Kind != UtilityValueKind.Named ||
            candidate.Modifier.Kind != UtilityModifierKind.Named ||
            !IsValidSpacingMultiplier(candidate.Value.Text) ||
            !IsValidSpacingMultiplier(candidate.Modifier.Text))
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "aspect-ratio",
                candidate.Value.Text + " / " + candidate.Modifier.Text));
        return true;
    }

    private static bool TryResolveAutomaticGridTrack(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null ||
            !IsValidSpacingMultiplier(candidate.Value.Text) ||
            !theme.TryGetSpacing(candidate.Value.Text, out var value))
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                candidate.Root == "auto-cols"
                    ? "grid-auto-columns"
                    : "grid-auto-rows",
                value!));
        return true;
    }

    private static bool TryResolveGridSpan(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        var property = candidate.Root == "col-span"
            ? "grid-column"
            : "grid-row";
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            if (!IsSafe(candidate.Value.Text))
            {
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    property,
                    "span " + candidate.Value.Text + " / span " + candidate.Value.Text));
            return true;
        }

        if (candidate.Value.Kind != UtilityValueKind.Named)
        {
            return false;
        }

        if (candidate.Value.Text == "full")
        {
            declarations.Add(new UtilityCssDeclaration(property, "1 / -1"));
            return true;
        }

        if (!IsCanonicalNonnegativeInteger(candidate.Value.Text))
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                property,
                "span " + candidate.Value.Text + " / span " + candidate.Value.Text));
        return true;
    }

    private static bool TryResolveArbitraryProperty(
        UtilityCandidate candidate,
        string property,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null ||
            !IsSafe(candidate.Value.Text))
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                property,
                candidate.Value.Text));
        return true;
    }

    private static bool TryResolveDecorationThickness(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "text-decoration-thickness",
                candidate.Value.Text == "0"
                    ? "0"
                    : candidate.Value.Text + "px"));
        return true;
    }

    private static bool TryResolveDivideStyle(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "--tw-border-style",
                candidate.Value.Text));
        declarations.Add(
            new UtilityCssDeclaration(
                "border-style",
                candidate.Value.Text));
        return true;
    }

    private static bool TryResolveFlex(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Value is null)
        {
            return false;
        }

        if (candidate.Modifier is null &&
            candidate.Value.Kind == UtilityValueKind.Named &&
            candidate.Value.Text is "wrap" or "nowrap" or "wrap-reverse")
        {
            declarations.Add(
                new UtilityCssDeclaration(
                    "flex-wrap",
                    candidate.Value.Text));
            return true;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = candidate.Modifier is null &&
                    IsSafe(candidate.Value.Text)
                ? candidate.Value.Text
                : null;
        }
        else if (candidate.Value.Fraction is not null &&
                 candidate.Modifier?.Kind == UtilityModifierKind.Named &&
                 IsCanonicalNonnegativeInteger(candidate.Value.Text) &&
                 IsCanonicalNonnegativeInteger(candidate.Modifier.Text))
        {
            value =
                "calc(" + candidate.Value.Text +
                " / " + candidate.Modifier.Text +
                " * 100%)";
        }
        else
        {
            value = candidate.Modifier is null &&
                    IsCanonicalNonnegativeInteger(candidate.Value.Text)
                ? candidate.Value.Text
                : null;
        }

        if (value is null)
        {
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("flex", value));
        return true;
    }

    private static bool TryResolveStaticFlex(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Value is not null ||
            candidate.Modifier is not null)
        {
            return false;
        }

        var value = candidate.Root switch
        {
            "flex-auto" => "auto",
            "flex-initial" => "0 auto",
            "flex-none" => "none",
            _ => null,
        };
        if (value is null)
        {
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("flex", value));
        return true;
    }

    private static bool TryResolveGridTrack(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafe(candidate.Value.Text)
                ? candidate.Value.Text
                : null;
        }
        else if (candidate.Value.Text is "none" or "subgrid")
        {
            value = candidate.Value.Text;
        }
        else if (IsCanonicalStrictPositiveInteger(candidate.Value.Text))
        {
            value =
                "repeat(" + candidate.Value.Text +
                ", minmax(0, 1fr))";
        }
        else
        {
            value = null;
        }

        if (value is null)
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                candidate.Root == "grid-cols"
                    ? "grid-template-columns"
                    : "grid-template-rows",
                value));
        return true;
    }

    private static bool TryResolveFontStretch(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        string? value;
        if (candidate.Value.Kind is UtilityValueKind.Arbitrary or UtilityValueKind.CssVariable)
        {
            value = IsSafe(candidate.Value.Text)
                ? candidate.Value.Text
                : null;
        }
        else if (candidate.Value.Text is
                     "ultra-condensed" or
                     "extra-condensed" or
                     "condensed" or
                     "semi-condensed" or
                     "normal" or
                     "semi-expanded" or
                     "expanded" or
                     "extra-expanded" or
                     "ultra-expanded")
        {
            value = candidate.Value.Text;
        }
        else
        {
            value = TryResolveFontStretchPercentage(candidate.Value.Text);
        }

        if (value is null)
        {
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("font-stretch", value));
        return true;
    }

    private static bool TryResolveObjectCorner(
        UtilityCandidate candidate,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Modifier is not null ||
            candidate.Value is null)
        {
            return false;
        }

        var value = candidate.Value.Text switch
        {
            "top-left" => "left top",
            "top-right" => "right top",
            "bottom-left" => "left bottom",
            "bottom-right" => "right bottom",
            _ => null,
        };
        if (value is null)
        {
            return false;
        }

        declarations.Add(new UtilityCssDeclaration("object-position", value));
        return true;
    }

    private static bool TryResolveArbitraryText(
        UtilityCandidate candidate,
        UtilityTheme theme,
        ICollection<UtilityCssDeclaration> declarations)
    {
        if (candidate.IsNegative ||
            candidate.Value is null ||
            !IsSafe(candidate.Value.Text))
        {
            return false;
        }

        if (IsArbitraryFontSize(candidate.Value))
        {
            if (!TryResolveLineHeight(
                    candidate.Modifier,
                    theme,
                    out var lineHeight))
            {
                return false;
            }

            declarations.Add(
                new UtilityCssDeclaration(
                    "font-size",
                    candidate.Value.Text));
            if (lineHeight is not null)
            {
                declarations.Add(
                    new UtilityCssDeclaration(
                        "line-height",
                        lineHeight));
            }

            return true;
        }

        if (!TryResolveOpacity(
                candidate.Modifier,
                out var opacity))
        {
            return false;
        }

        declarations.Add(
            new UtilityCssDeclaration(
                "color",
                opacity is null
                    ? candidate.Value.Text
                    : "color-mix(in oklab, " +
                      candidate.Value.Text + " " +
                      opacity + ", transparent)"));
        return true;
    }

    private static bool TryResolveLineHeight(
        UtilityModifier? modifier,
        UtilityTheme theme,
        out string? value)
    {
        value = null;
        if (modifier is null)
        {
            return true;
        }

        if (modifier.Kind is UtilityModifierKind.Arbitrary or UtilityModifierKind.CssVariable)
        {
            value = IsSafe(modifier.Text)
                ? modifier.Text
                : null;
            return value is not null;
        }

        if (theme.TryGetNamespaceValue(
                UtilityThemeNamespaceNames.LineHeight,
                modifier.Text,
                out value))
        {
            return true;
        }

        if (IsValidSpacingMultiplier(modifier.Text) &&
            theme.TryGetSpacing(modifier.Text, out value))
        {
            return true;
        }

        if (modifier.Text == "none")
        {
            value = "1";
            return true;
        }

        return false;
    }

    private static bool TryResolveOpacity(
        UtilityModifier? modifier,
        out string? value)
    {
        value = null;
        if (modifier is null)
        {
            return true;
        }

        if (modifier.Kind is UtilityModifierKind.Arbitrary or UtilityModifierKind.CssVariable)
        {
            if (!IsSafe(modifier.Text))
            {
                return false;
            }

            value = modifier.Text.EndsWith("%", StringComparison.Ordinal)
                ? modifier.Text
                : "calc(" + modifier.Text + " * 100%)";
            return true;
        }

        if (!IsCanonicalNumber(modifier.Text, out var number) ||
            number < decimal.Zero ||
            number > 100m ||
            number % 0.25m != decimal.Zero)
        {
            return false;
        }

        value = number.ToString(
            "0.############################",
            CultureInfo.InvariantCulture) + "%";
        return true;
    }

    private static bool IsArbitraryFontSize(UtilityValue value)
    {
        if (value.DataType is not null)
        {
            return value.DataType is
                "size" or
                "length" or
                "percentage" or
                "absolute-size" or
                "relative-size";
        }

        var text = value.Text;
        if (text is
            "xx-small" or
            "x-small" or
            "small" or
            "medium" or
            "large" or
            "x-large" or
            "xx-large" or
            "xxx-large" or
            "larger" or
            "smaller")
        {
            return true;
        }

        if (text.EndsWith("%", StringComparison.Ordinal) ||
            StartsWithMathFunction(text) ||
            text.StartsWith("--spacing(", StringComparison.Ordinal))
        {
            return true;
        }

        var unitStart = 0;
        if (text.Length > 0 &&
            (text[0] == '+' || text[0] == '-'))
        {
            unitStart = 1;
        }

        while (unitStart < text.Length &&
               (char.IsDigit(text[unitStart]) || text[unitStart] == '.'))
        {
            unitStart++;
        }

        return unitStart > 0 &&
            unitStart < text.Length &&
            char.IsLetter(text[unitStart]);
    }

    private static bool StartsWithMathFunction(string value) =>
        value.StartsWith("calc(", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("clamp(", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("max(", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("min(", StringComparison.OrdinalIgnoreCase);

    private static string? TryResolveFontStretchPercentage(string value)
    {
        if (!value.EndsWith("%", StringComparison.Ordinal))
        {
            return null;
        }

        var numberText = value.Substring(0, value.Length - 1);
        if (!IsCanonicalNonnegativeInteger(numberText) ||
            !decimal.TryParse(
                numberText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number) ||
            number < 50m ||
            number > 200m)
        {
            return null;
        }

        return value;
    }

    private static bool IsValidSpacingMultiplier(string value) =>
        IsCanonicalNumber(value, out var number) &&
        number >= decimal.Zero &&
        number % 0.25m == decimal.Zero;

    private static bool IsCanonicalStrictPositiveInteger(string value) =>
        IsCanonicalInteger(value, out var number) &&
        number > decimal.Zero;

    private static bool IsCanonicalNonnegativeInteger(string value) =>
        IsCanonicalInteger(value, out var number) &&
        number >= decimal.Zero;

    private static bool IsCanonicalInteger(
        string value,
        out decimal number) =>
        IsCanonicalNumber(value, out number) &&
        decimal.Truncate(number) == number;

    private static bool IsCanonicalNumber(
        string value,
        out decimal number)
    {
        if (!decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out number))
        {
            return false;
        }

        return value == number.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
    }

    private static bool IsSafe(string value) =>
        !string.IsNullOrEmpty(value) &&
        UtilityCssText.IsSafeValue(value);
}
