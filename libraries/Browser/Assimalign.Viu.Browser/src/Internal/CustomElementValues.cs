using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assimalign.Viu.Browser;

// Explicit type-token dispatch over ComponentParameter metadata: no member discovery [CEL-3].
internal static class CustomElementValues
{
    internal static string Hyphenate(string name)
    {
        StringBuilder result = new();
        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (char.IsUpper(character))
            {
                if (index > 0 && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1])
                    || (index + 1 < name.Length && char.IsLower(name[index + 1]))))
                {
                    result.Append('-');
                }
                result.Append(char.ToLowerInvariant(character));
            }
            else
            {
                result.Append(character);
            }
        }
        return result.ToString();
    }

    internal static void ValidateTagName(string tagName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tagName);
        // WHATWG potential-custom-element-name, including PCENChar's supplementary range.
        // https://html.spec.whatwg.org/multipage/custom-elements.html#valid-custom-element-name
        bool valid = tagName[0] is >= 'a' and <= 'z' && tagName.Contains('-', StringComparison.Ordinal);
        foreach (Rune character in tagName.EnumerateRunes())
        {
            int value = character.Value;
            valid &= value is '-' or '.' or '_' or 0xB7
                or >= '0' and <= '9' or >= 'a' and <= 'z'
                or >= 0xC0 and <= 0xD6 or >= 0xD8 and <= 0xF6 or >= 0xF8 and <= 0x37D
                or >= 0x37F and <= 0x1FFF or >= 0x200C and <= 0x200D
                or >= 0x203F and <= 0x2040 or >= 0x2070 and <= 0x218F
                or >= 0x2C00 and <= 0x2FEF or >= 0x3001 and <= 0xD7FF
                or >= 0xF900 and <= 0xFDCF or >= 0xFDF0 and <= 0xFFFD
                or >= 0x10000 and <= 0xEFFFF;
        }
        valid &= tagName is not ("annotation-xml" or "color-profile" or "font-face"
            or "font-face-src" or "font-face-uri" or "font-face-format" or "font-face-name" or "missing-glyph");
        if (!valid)
        {
            throw new ArgumentException("A custom element requires a valid lowercase WHATWG name containing a hyphen.", nameof(tagName));
        }
    }

    internal static void ValidateAttributeName(
        string attributeName,
        ISet<string> observedNames,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(attributeName);
        ArgumentNullException.ThrowIfNull(observedNames);
        if (attributeName.Length > 0
            && (attributeName != attributeName.ToLowerInvariant()
                || attributeName.IndexOfAny(
                    [' ', '\t', '\r', '\n', '\f', '\0', '"', '\'', '>', '<', '/', '=']) >= 0
                || !observedNames.Add(attributeName)))
        {
            throw new ArgumentException(
                $"Invalid or duplicate custom-element attribute '{attributeName}'.",
                parameterName);
        }
    }

    internal static Type? ValueType(Type? type) => type == typeof(bool?) ? typeof(bool)
        : type == typeof(byte?) ? typeof(byte) : type == typeof(sbyte?) ? typeof(sbyte)
        : type == typeof(short?) ? typeof(short) : type == typeof(ushort?) ? typeof(ushort)
        : type == typeof(int?) ? typeof(int) : type == typeof(uint?) ? typeof(uint)
        : type == typeof(long?) ? typeof(long) : type == typeof(ulong?) ? typeof(ulong)
        : type == typeof(float?) ? typeof(float) : type == typeof(double?) ? typeof(double)
        : type == typeof(decimal?) ? typeof(decimal) : type == typeof(Half?) ? typeof(Half)
        : type == typeof(Int128?) ? typeof(Int128) : type == typeof(UInt128?) ? typeof(UInt128)
        : type == typeof(nint?) ? typeof(nint) : type == typeof(nuint?) ? typeof(nuint) : type;

    internal static bool IsNumeric(Type? type) => type == typeof(byte) || type == typeof(sbyte)
        || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
        || type == typeof(long) || type == typeof(ulong) || type == typeof(float) || type == typeof(double)
        || type == typeof(decimal) || type == typeof(Half) || type == typeof(Int128) || type == typeof(UInt128)
        || type == typeof(nint) || type == typeof(nuint);

    internal static bool IsReflectable(Type? type)
    {
        type = ValueType(type);
        return type == typeof(string) || type == typeof(bool) || IsNumeric(type);
    }

    internal static bool TryAttribute(Type? type, string? text, out object? value)
    {
        type = ValueType(type);
        value = null;
        if (type == typeof(bool))
        {
            value = text is not null && text != "false";
            return true;
        }

        if (text is null)
        {
            return false;
        }

        if (type == typeof(string))
        {
            value = text;
            return true;
        }

        return TryNumber(type, text, out value);
    }

    internal static bool TryNumber(Type? type, string text, out object? value)
    {
        value = null;
        const NumberStyles integral = NumberStyles.Integer;
        const NumberStyles floating = NumberStyles.Float;
        CultureInfo culture = CultureInfo.InvariantCulture;

        if (type == typeof(byte)
            && byte.TryParse(text, integral, culture, out byte byteValue))
        {
            value = byteValue;
            return true;
        }

        if (type == typeof(sbyte)
            && sbyte.TryParse(text, integral, culture, out sbyte signedByteValue))
        {
            value = signedByteValue;
            return true;
        }

        if (type == typeof(short)
            && short.TryParse(text, integral, culture, out short shortValue))
        {
            value = shortValue;
            return true;
        }

        if (type == typeof(ushort)
            && ushort.TryParse(text, integral, culture, out ushort unsignedShortValue))
        {
            value = unsignedShortValue;
            return true;
        }

        if (type == typeof(int)
            && int.TryParse(text, integral, culture, out int integerValue))
        {
            value = integerValue;
            return true;
        }

        if (type == typeof(uint)
            && uint.TryParse(text, integral, culture, out uint unsignedIntegerValue))
        {
            value = unsignedIntegerValue;
            return true;
        }

        if (type == typeof(long)
            && long.TryParse(text, integral, culture, out long longValue))
        {
            value = longValue;
            return true;
        }

        if (type == typeof(ulong)
            && ulong.TryParse(text, integral, culture, out ulong unsignedLongValue))
        {
            value = unsignedLongValue;
            return true;
        }

        if (type == typeof(nint)
            && nint.TryParse(text, integral, culture, out nint nativeIntegerValue))
        {
            value = nativeIntegerValue;
            return true;
        }

        if (type == typeof(nuint)
            && nuint.TryParse(text, integral, culture, out nuint unsignedNativeIntegerValue))
        {
            value = unsignedNativeIntegerValue;
            return true;
        }

        if (type == typeof(Int128)
            && Int128.TryParse(text, integral, culture, out Int128 integer128Value))
        {
            value = integer128Value;
            return true;
        }

        if (type == typeof(UInt128)
            && UInt128.TryParse(text, integral, culture, out UInt128 unsignedInteger128Value))
        {
            value = unsignedInteger128Value;
            return true;
        }

        if (type == typeof(float)
            && float.TryParse(text, floating, culture, out float singleValue)
            && float.IsFinite(singleValue))
        {
            value = singleValue;
            return true;
        }

        if (type == typeof(double)
            && double.TryParse(text, floating, culture, out double doubleValue)
            && double.IsFinite(doubleValue))
        {
            value = doubleValue;
            return true;
        }

        if (type == typeof(Half)
            && Half.TryParse(text, floating, culture, out Half halfValue)
            && Half.IsFinite(halfValue))
        {
            value = halfValue;
            return true;
        }

        if (type == typeof(decimal)
            && decimal.TryParse(text, floating, culture, out decimal decimalValue))
        {
            value = decimalValue;
            return true;
        }

        return false;
    }

    internal static bool TryProperty(Type? type, object? input, out object? value)
    {
        value = null;
        Type? effectiveType = ValueType(type);
        if (input is null)
        {
            return type is null || type == typeof(object) || type == typeof(string) || type != effectiveType;
        }

        if (type is null || type == typeof(object))
        {
            value = input;
            return true;
        }

        if (effectiveType == typeof(string) && input is string)
        {
            value = input;
            return true;
        }

        if (effectiveType == typeof(bool) && input is bool)
        {
            value = input;
            return true;
        }

        if (IsNumeric(effectiveType) && input is double number && double.IsFinite(number))
        {
            // JS has one Number type. This normalizes its representation, never parses strings.
            string text = number.ToString("R", CultureInfo.InvariantCulture);
            if (effectiveType != typeof(float) && effectiveType != typeof(double)
                && effectiveType != typeof(Half) && effectiveType != typeof(decimal))
            {
                if (Math.Truncate(number) != number
                    || Math.Abs(number) > 9007199254740991d)
                {
                    return false;
                }

                text = number.ToString("0", CultureInfo.InvariantCulture);
            }

            return TryNumber(effectiveType, text, out value);
        }

        return false;
    }
}
