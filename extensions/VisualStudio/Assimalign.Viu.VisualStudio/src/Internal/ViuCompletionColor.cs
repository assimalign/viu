using System;
using System.Globalization;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Represents the device-sRGB color used to draw one completion swatch.
/// </summary>
internal readonly struct ViuCompletionColor : IEquatable<ViuCompletionColor>
{
    private const double OklchPercentageChromaScale = 0.4;

    /// <summary>Initializes a color from its eight-bit channels.</summary>
    internal ViuCompletionColor(byte red, byte green, byte blue, byte alpha)
    {
        this.Red = red;
        this.Green = green;
        this.Blue = blue;
        this.Alpha = alpha;
    }

    /// <summary>Gets the red channel.</summary>
    internal byte Red { get; }

    /// <summary>Gets the green channel.</summary>
    internal byte Green { get; }

    /// <summary>Gets the blue channel.</summary>
    internal byte Blue { get; }

    /// <summary>Gets the alpha channel.</summary>
    internal byte Alpha { get; }

    /// <summary>
    /// Parses the concrete CSS color value transported in a completion payload.
    /// </summary>
    internal static bool TryParse(string? value, out ViuCompletionColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string text = value!.Trim();
        if (TryParseHex(text, out color) ||
            TryParseRgb(text, out color) ||
            TryParseHsl(text, out color) ||
            TryParseOklch(text, out color))
        {
            return true;
        }

        switch (text.ToLowerInvariant())
        {
            case "black":
                color = new ViuCompletionColor(0, 0, 0, 255);
                return true;
            case "white":
                color = new ViuCompletionColor(255, 255, 255, 255);
                return true;
            case "red":
                color = new ViuCompletionColor(255, 0, 0, 255);
                return true;
            case "green":
                color = new ViuCompletionColor(0, 128, 0, 255);
                return true;
            case "blue":
                color = new ViuCompletionColor(0, 0, 255, 255);
                return true;
            case "rebeccapurple":
                color = new ViuCompletionColor(102, 51, 153, 255);
                return true;
            case "transparent":
                color = new ViuCompletionColor(0, 0, 0, 0);
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc />
    public bool Equals(ViuCompletionColor other) =>
        this.Red == other.Red &&
        this.Green == other.Green &&
        this.Blue == other.Blue &&
        this.Alpha == other.Alpha;

    /// <inheritdoc />
    public override bool Equals(object? value) =>
        value is ViuCompletionColor other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        (((this.Red * 397) ^ this.Green) * 397 ^ this.Blue) * 397 ^ this.Alpha;

    private static bool TryParseHex(string text, out ViuCompletionColor color)
    {
        color = default;
        if (text.Length < 4 || text[0] != '#')
        {
            return false;
        }

        string digits = text.Substring(1);
        byte red;
        byte green;
        byte blue;
        byte alpha = 255;

        if (digits.Length is 3 or 4)
        {
            if (!TryParseHexByte(new string(digits[0], 2), out red) ||
                !TryParseHexByte(new string(digits[1], 2), out green) ||
                !TryParseHexByte(new string(digits[2], 2), out blue) ||
                (digits.Length == 4 &&
                 !TryParseHexByte(new string(digits[3], 2), out alpha)))
            {
                return false;
            }
        }
        else if (digits.Length is 6 or 8)
        {
            if (!TryParseHexByte(digits.Substring(0, 2), out red) ||
                !TryParseHexByte(digits.Substring(2, 2), out green) ||
                !TryParseHexByte(digits.Substring(4, 2), out blue) ||
                (digits.Length == 8 && !TryParseHexByte(digits.Substring(6, 2), out alpha)))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        color = new ViuCompletionColor(red, green, blue, alpha);
        return true;
    }

    private static bool TryParseRgb(string text, out ViuCompletionColor color)
    {
        color = default;
        if (!TryGetFunctionBody(text, "rgb", out string body) &&
            !TryGetFunctionBody(text, "rgba", out body))
        {
            return false;
        }

        SplitFunctionBody(body, out string componentsText, out string? alphaText);
        string[] components = SplitComponents(componentsText);
        if (components.Length == 4 && alphaText is null)
        {
            alphaText = components[3];
        }

        if (components.Length is not (3 or 4) ||
            !TryParseRgbChannel(components[0], out byte red) ||
            !TryParseRgbChannel(components[1], out byte green) ||
            !TryParseRgbChannel(components[2], out byte blue) ||
            !TryParseAlpha(alphaText, out byte alpha))
        {
            return false;
        }

        color = new ViuCompletionColor(red, green, blue, alpha);
        return true;
    }

    private static bool TryParseHsl(string text, out ViuCompletionColor color)
    {
        color = default;
        if (!TryGetFunctionBody(text, "hsl", out string body) &&
            !TryGetFunctionBody(text, "hsla", out body))
        {
            return false;
        }

        SplitFunctionBody(body, out string componentsText, out string? alphaText);
        string[] components = SplitComponents(componentsText);
        if (components.Length == 4 && alphaText is null)
        {
            alphaText = components[3];
        }

        if (components.Length is not (3 or 4) ||
            !TryParseHue(components[0], out double hue) ||
            !TryParsePercentage(components[1], out double saturation) ||
            !TryParsePercentage(components[2], out double lightness) ||
            !TryParseAlpha(alphaText, out byte alpha))
        {
            return false;
        }

        double chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        double sector = hue / 60;
        double intermediate = chroma * (1 - Math.Abs((sector % 2) - 1));
        double redComponent;
        double greenComponent;
        double blueComponent;

        if (sector < 1)
        {
            redComponent = chroma;
            greenComponent = intermediate;
            blueComponent = 0;
        }
        else if (sector < 2)
        {
            redComponent = intermediate;
            greenComponent = chroma;
            blueComponent = 0;
        }
        else if (sector < 3)
        {
            redComponent = 0;
            greenComponent = chroma;
            blueComponent = intermediate;
        }
        else if (sector < 4)
        {
            redComponent = 0;
            greenComponent = intermediate;
            blueComponent = chroma;
        }
        else if (sector < 5)
        {
            redComponent = intermediate;
            greenComponent = 0;
            blueComponent = chroma;
        }
        else
        {
            redComponent = chroma;
            greenComponent = 0;
            blueComponent = intermediate;
        }

        double match = lightness - (chroma / 2);
        color = new ViuCompletionColor(
            ToByte(redComponent + match),
            ToByte(greenComponent + match),
            ToByte(blueComponent + match),
            alpha);
        return true;
    }

    private static bool TryParseOklch(string text, out ViuCompletionColor color)
    {
        color = default;
        if (!TryGetFunctionBody(text, "oklch", out string body))
        {
            return false;
        }

        SplitFunctionBody(body, out string componentsText, out string? alphaText);
        string[] components = SplitComponents(componentsText);
        if (components.Length != 3 ||
            !TryParseLightness(components[0], out double lightness) ||
            !TryParseChroma(components[1], out double chroma) ||
            !TryParseHue(components[2], out double hue) ||
            !TryParseAlpha(alphaText, out byte alpha))
        {
            return false;
        }

        double radians = hue * Math.PI / 180;
        double a = chroma * Math.Cos(radians);
        double b = chroma * Math.Sin(radians);

        double lBase = lightness + (0.3963377774 * a) + (0.2158037573 * b);
        double mBase = lightness - (0.1055613458 * a) - (0.0638541728 * b);
        double sBase = lightness - (0.0894841775 * a) - (1.2914855480 * b);
        double l = lBase * lBase * lBase;
        double m = mBase * mBase * mBase;
        double s = sBase * sBase * sBase;

        double redLinear = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        double greenLinear = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        double blueLinear = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

        color = new ViuCompletionColor(
            ToByte(LinearToSrgb(redLinear)),
            ToByte(LinearToSrgb(greenLinear)),
            ToByte(LinearToSrgb(blueLinear)),
            alpha);
        return true;
    }

    private static bool TryGetFunctionBody(string text, string name, out string body)
    {
        body = string.Empty;
        string prefix = name + "(";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            text[text.Length - 1] != ')')
        {
            return false;
        }

        body = text.Substring(prefix.Length, text.Length - prefix.Length - 1).Trim();
        return body.Length > 0;
    }

    private static void SplitFunctionBody(
        string body,
        out string components,
        out string? alpha)
    {
        int separator = body.IndexOf('/');
        if (separator < 0)
        {
            components = body;
            alpha = null;
            return;
        }

        components = body.Substring(0, separator).Trim();
        alpha = body.Substring(separator + 1).Trim();
    }

    private static string[] SplitComponents(string text) =>
        text.Replace(',', ' ').Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

    private static bool TryParseHexByte(string text, out byte value) =>
        byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

    private static bool TryParseRgbChannel(string text, out byte value)
    {
        value = 0;
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            if (!TryParseNumber(text.Substring(0, text.Length - 1), out double percentage))
            {
                return false;
            }

            value = ToByte(percentage / 100);
            return true;
        }

        if (!TryParseNumber(text, out double channel))
        {
            return false;
        }

        value = ToByte(channel / 255);
        return true;
    }

    private static bool TryParseAlpha(string? text, out byte alpha)
    {
        alpha = 255;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (text!.EndsWith("%", StringComparison.Ordinal))
        {
            if (!TryParseNumber(text.Substring(0, text.Length - 1), out double percentage))
            {
                return false;
            }

            alpha = ToByte(percentage / 100);
            return true;
        }

        if (!TryParseNumber(text, out double value))
        {
            return false;
        }

        alpha = ToByte(value);
        return true;
    }

    private static bool TryParseLightness(string text, out double value)
    {
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            if (!TryParseNumber(text.Substring(0, text.Length - 1), out value))
            {
                return false;
            }

            value = Clamp(value / 100);
            return true;
        }

        if (!TryParseNumber(text, out value))
        {
            return false;
        }

        value = Clamp(value);
        return true;
    }

    private static bool TryParseChroma(string text, out double value)
    {
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            if (!TryParseNumber(text.Substring(0, text.Length - 1), out value))
            {
                return false;
            }

            value = Math.Max(0, value / 100 * OklchPercentageChromaScale);
            return true;
        }

        if (!TryParseNumber(text, out value))
        {
            return false;
        }

        value = Math.Max(0, value);
        return true;
    }

    private static bool TryParseHue(string text, out double degrees)
    {
        double multiplier = 1;
        string number = text;
        if (text.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        {
            number = text.Substring(0, text.Length - 3);
        }
        else if (text.EndsWith("grad", StringComparison.OrdinalIgnoreCase))
        {
            number = text.Substring(0, text.Length - 4);
            multiplier = 0.9;
        }
        else if (text.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
        {
            number = text.Substring(0, text.Length - 3);
            multiplier = 180 / Math.PI;
        }
        else if (text.EndsWith("turn", StringComparison.OrdinalIgnoreCase))
        {
            number = text.Substring(0, text.Length - 4);
            multiplier = 360;
        }

        if (!TryParseNumber(number, out degrees))
        {
            return false;
        }

        degrees = ((degrees * multiplier) % 360 + 360) % 360;
        return true;
    }

    private static bool TryParsePercentage(string text, out double value)
    {
        value = 0;
        if (!text.EndsWith("%", StringComparison.Ordinal) ||
            !TryParseNumber(text.Substring(0, text.Length - 1), out double percentage))
        {
            return false;
        }

        value = Clamp(percentage / 100);
        return true;
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture,
            out value) &&
        !double.IsNaN(value) &&
        !double.IsInfinity(value);

    private static double LinearToSrgb(double value) =>
        value <= 0.0031308
            ? 12.92 * value
            : (1.055 * Math.Pow(value, 1 / 2.4)) - 0.055;

    private static byte ToByte(double value) =>
        (byte)Math.Round(Clamp(value) * 255, MidpointRounding.AwayFromZero);

    private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
}
