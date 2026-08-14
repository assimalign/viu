using System;
using System.Globalization;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal static class CssColorParser
{
    internal static bool TryParse(string value, out CssColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text[0] == '#')
        {
            return TryParseHexadecimal(text, out color);
        }

        if (TryGetFunctionBody(text, "rgb", out var redGreenBlueBody) ||
            TryGetFunctionBody(text, "rgba", out redGreenBlueBody))
        {
            return TryParseRedGreenBlue(redGreenBlueBody, out color);
        }

        if (TryGetFunctionBody(text, "hsl", out var hueSaturationLightnessBody) ||
            TryGetFunctionBody(text, "hsla", out hueSaturationLightnessBody))
        {
            return TryParseHueSaturationLightness(hueSaturationLightnessBody, out color);
        }

        if (TryGetFunctionBody(text, "oklch", out var perceptualColorBody))
        {
            return TryParsePerceptualLightnessChromaHue(perceptualColorBody, out color);
        }

        if (TryGetFunctionBody(text, "color-mix", out var colorMixBody))
        {
            return TryParseColorMixWithTransparent(colorMixBody, out color);
        }

        return TryParseNamed(text, out color);
    }

    internal static string ToHexadecimal(CssColor color)
    {
        var red = ToByte(color.Red);
        var green = ToByte(color.Green);
        var blue = ToByte(color.Blue);
        var alpha = ToByte(color.Alpha);
        return alpha == byte.MaxValue
            ? FormattableString.Invariant($"#{red:x2}{green:x2}{blue:x2}")
            : FormattableString.Invariant($"#{red:x2}{green:x2}{blue:x2}{alpha:x2}");
    }

    private static bool TryParseHexadecimal(string text, out CssColor color)
    {
        color = default;
        var hexadecimal = text.AsSpan(1);
        if (hexadecimal.Length is not (3 or 4 or 6 or 8))
        {
            return false;
        }

        if (hexadecimal.Length <= 4)
        {
            var alpha = 15;
            if (!TryParseHexadecimalDigit(hexadecimal[0], out var red) ||
                !TryParseHexadecimalDigit(hexadecimal[1], out var green) ||
                !TryParseHexadecimalDigit(hexadecimal[2], out var blue) ||
                (hexadecimal.Length == 4 &&
                 !TryParseHexadecimalDigit(hexadecimal[3], out alpha)))
            {
                return false;
            }

            color = new CssColor(
                red / 15.0,
                green / 15.0,
                blue / 15.0,
                alpha / 15.0);
            return true;
        }

        var alphaByte = byte.MaxValue;
        if (!byte.TryParse(
                hexadecimal.Slice(0, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var redByte) ||
            !byte.TryParse(
                hexadecimal.Slice(2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var greenByte) ||
            !byte.TryParse(
                hexadecimal.Slice(4, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var blueByte) ||
            (hexadecimal.Length == 8 &&
             !byte.TryParse(
                 hexadecimal.Slice(6, 2),
                 NumberStyles.HexNumber,
                 CultureInfo.InvariantCulture,
                 out alphaByte)))
        {
            return false;
        }

        color = new CssColor(
            redByte / 255.0,
            greenByte / 255.0,
            blueByte / 255.0,
            alphaByte / 255.0);
        return true;
    }

    private static bool TryParseRedGreenBlue(string body, out CssColor color)
    {
        color = default;
        var parts = SplitComponents(body);
        var alpha = 1.0;
        if (parts.Length is not (3 or 4) ||
            !TryParseRedGreenBlueChannel(parts[0], out var red) ||
            !TryParseRedGreenBlueChannel(parts[1], out var green) ||
            !TryParseRedGreenBlueChannel(parts[2], out var blue) ||
            (parts.Length == 4 && !TryParseAlpha(parts[3], out alpha)))
        {
            return false;
        }

        color = new CssColor(
            red,
            green,
            blue,
            alpha);
        return true;
    }

    private static bool TryParseHueSaturationLightness(
        string body,
        out CssColor color)
    {
        color = default;
        var parts = SplitComponents(body);
        var alpha = 1.0;
        if (parts.Length is not (3 or 4) ||
            !TryParseHue(parts[0], out var hue) ||
            !TryParsePercentage(parts[1], out var saturation) ||
            !TryParsePercentage(parts[2], out var lightness) ||
            (parts.Length == 4 && !TryParseAlpha(parts[3], out alpha)))
        {
            return false;
        }

        var chroma = (1.0 - Math.Abs((2.0 * lightness) - 1.0)) * saturation;
        var hueSection = hue / 60.0;
        var secondary = chroma * (1.0 - Math.Abs((hueSection % 2.0) - 1.0));
        var (redPrime, greenPrime, bluePrime) = hueSection switch
        {
            < 1.0 => (chroma, secondary, 0.0),
            < 2.0 => (secondary, chroma, 0.0),
            < 3.0 => (0.0, chroma, secondary),
            < 4.0 => (0.0, secondary, chroma),
            < 5.0 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary),
        };
        var match = lightness - (chroma / 2.0);
        color = new CssColor(
            Clamp(redPrime + match),
            Clamp(greenPrime + match),
            Clamp(bluePrime + match),
            alpha);
        return true;
    }

    private static bool TryParsePerceptualLightnessChromaHue(
        string body,
        out CssColor color)
    {
        color = default;
        var parts = SplitComponents(body);
        var alpha = 1.0;
        if (parts.Length is not (3 or 4) ||
            !TryParseLightness(parts[0], out var lightness) ||
            !TryParseNumber(parts[1], out var chroma) ||
            !TryParseHue(parts[2], out var hue) ||
            (parts.Length == 4 && !TryParseAlpha(parts[3], out alpha)))
        {
            return false;
        }

        var radians = hue * Math.PI / 180.0;
        var greenRedOpponent = chroma * Math.Cos(radians);
        var blueYellowOpponent = chroma * Math.Sin(radians);
        var longConePrime =
            lightness +
            (0.3963377774 * greenRedOpponent) +
            (0.2158037573 * blueYellowOpponent);
        var mediumConePrime =
            lightness -
            (0.1055613458 * greenRedOpponent) -
            (0.0638541728 * blueYellowOpponent);
        var shortConePrime =
            lightness -
            (0.0894841775 * greenRedOpponent) -
            (1.2914855480 * blueYellowOpponent);
        var longCone = longConePrime * longConePrime * longConePrime;
        var mediumCone = mediumConePrime * mediumConePrime * mediumConePrime;
        var shortCone = shortConePrime * shortConePrime * shortConePrime;
        var linearRed =
            (4.0767416621 * longCone) -
            (3.3077115913 * mediumCone) +
            (0.2309699292 * shortCone);
        var linearGreen =
            (-1.2684380046 * longCone) +
            (2.6097574011 * mediumCone) -
            (0.3413193965 * shortCone);
        var linearBlue =
            (-0.0041960863 * longCone) -
            (0.7034186147 * mediumCone) +
            (1.7076147010 * shortCone);
        color = new CssColor(
            ToStandardRedGreenBlue(linearRed),
            ToStandardRedGreenBlue(linearGreen),
            ToStandardRedGreenBlue(linearBlue),
            alpha);
        return true;
    }

    private static bool TryParseNamed(string text, out CssColor color)
    {
        color = text.ToLowerInvariant() switch
        {
            "transparent" => new CssColor(0, 0, 0, 0),
            "black" => new CssColor(0, 0, 0, 1),
            "white" => new CssColor(1, 1, 1, 1),
            "red" => new CssColor(1, 0, 0, 1),
            "green" => new CssColor(0, 128 / 255.0, 0, 1),
            "blue" => new CssColor(0, 0, 1, 1),
            "yellow" => new CssColor(1, 1, 0, 1),
            "magenta" or "fuchsia" => new CssColor(1, 0, 1, 1),
            "cyan" or "aqua" => new CssColor(0, 1, 1, 1),
            "gray" or "grey" => new CssColor(128 / 255.0, 128 / 255.0, 128 / 255.0, 1),
            "rebeccapurple" => new CssColor(102 / 255.0, 51 / 255.0, 153 / 255.0, 1),
            _ => default,
        };
        return text.Equals("transparent", StringComparison.OrdinalIgnoreCase) ||
               color.Alpha > 0;
    }

    private static bool TryParseColorMixWithTransparent(
        string body,
        out CssColor color)
    {
        const string InterpolationMethod = "in oklab,";
        color = default;
        if (!body.StartsWith(
                InterpolationMethod,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var arguments = body.Substring(InterpolationMethod.Length).Trim();
        var separator = FindTopLevelComma(arguments);
        if (separator < 0 ||
            !arguments.Substring(separator + 1)
                .Trim()
                .Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var weightedColor = arguments.Substring(0, separator).Trim();
        var percentageSeparator = FindLastTopLevelWhitespace(weightedColor);
        if (percentageSeparator < 0 ||
            !TryParsePercentage(
                weightedColor.Substring(percentageSeparator + 1).Trim(),
                out var percentage) ||
            !TryParse(
                weightedColor.Substring(0, percentageSeparator).Trim(),
                out var baseColor))
        {
            return false;
        }

        color = baseColor with
        {
            Alpha = Clamp(baseColor.Alpha * percentage),
        };
        return true;
    }

    private static int FindTopLevelComma(string text)
    {
        var parenthesisDepth = 0;
        for (var index = 0; index < text.Length; index++)
        {
            parenthesisDepth += text[index] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (text[index] == ',' && parenthesisDepth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindLastTopLevelWhitespace(string text)
    {
        var parenthesisDepth = 0;
        for (var index = text.Length - 1; index >= 0; index--)
        {
            parenthesisDepth += text[index] switch
            {
                ')' => 1,
                '(' => -1,
                _ => 0,
            };
            if (char.IsWhiteSpace(text[index]) && parenthesisDepth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryGetFunctionBody(
        string text,
        string functionName,
        out string body)
    {
        body = string.Empty;
        if (!text.StartsWith(
                functionName + "(",
                StringComparison.OrdinalIgnoreCase) ||
            text[^1] != ')')
        {
            return false;
        }

        body = text.Substring(functionName.Length + 1, text.Length - functionName.Length - 2);
        return true;
    }

    private static string[] SplitComponents(string body)
        => body
            .Replace(',', ' ')
            .Replace("/", " / ", StringComparison.Ordinal)
            .Split(
                [' ', '\t', '\r', '\n', '/'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryParseRedGreenBlueChannel(string text, out double value)
    {
        if (TryParsePercentage(text, out value))
        {
            return true;
        }

        if (!TryParseNumber(text, out var numeric))
        {
            value = 0;
            return false;
        }

        value = Clamp(numeric / 255.0);
        return true;
    }

    private static bool TryParseAlpha(string text, out double value)
    {
        if (TryParsePercentage(text, out value))
        {
            return true;
        }

        if (!TryParseNumber(text, out value))
        {
            return false;
        }

        value = Clamp(value);
        return true;
    }

    private static bool TryParseLightness(string text, out double value)
    {
        if (TryParsePercentage(text, out value))
        {
            return true;
        }

        if (!TryParseNumber(text, out value))
        {
            return false;
        }

        value = Clamp(value);
        return true;
    }

    private static bool TryParsePercentage(string text, out double value)
    {
        value = 0;
        if (!text.EndsWith('%') ||
            !TryParseNumber(text.Substring(0, text.Length - 1), out var percentage))
        {
            return false;
        }

        value = Clamp(percentage / 100.0);
        return true;
    }

    private static bool TryParseHue(string text, out double degrees)
    {
        var multiplier = 1.0;
        var numericText = text;
        if (text.EndsWith("turn", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 360.0;
            numericText = text.Substring(0, text.Length - 4);
        }
        else if (text.EndsWith("grad", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 0.9;
            numericText = text.Substring(0, text.Length - 4);
        }
        else if (text.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 180.0 / Math.PI;
            numericText = text.Substring(0, text.Length - 3);
        }
        else if (text.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        {
            numericText = text.Substring(0, text.Length - 3);
        }

        if (!TryParseNumber(numericText, out var numeric))
        {
            degrees = 0;
            return false;
        }

        degrees = ((numeric * multiplier) % 360.0 + 360.0) % 360.0;
        return true;
    }

    private static bool TryParseNumber(string text, out double value)
        => double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value) &&
           double.IsFinite(value);

    private static bool TryParseHexadecimalDigit(char value, out int parsed)
    {
        parsed = value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1,
        };
        return parsed >= 0;
    }

    private static double ToStandardRedGreenBlue(double linear)
        => Clamp(
            linear <= 0.0031308
                ? 12.92 * linear
                : (1.055 * Math.Pow(linear, 1.0 / 2.4)) - 0.055);

    private static byte ToByte(double value)
        => (byte)Math.Round(
            Clamp(value) * byte.MaxValue,
            MidpointRounding.AwayFromZero);

    private static double Clamp(double value) => Math.Clamp(value, 0.0, 1.0);
}
