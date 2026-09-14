using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Router;

/// <summary>Parses and writes the form-encoded query representation specified by [RTR-12].</summary>
internal static class RouteQueryEncoding
{
    private const string HexadecimalDigits = "0123456789ABCDEF";

    internal static KeyValuePair<string, string>[] Parse(ReadOnlySpan<char> rawQuery)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        while (!rawQuery.IsEmpty)
        {
            var separator = rawQuery.IndexOf('&');
            var segment = separator < 0 ? rawQuery : rawQuery[..separator];
            if (!segment.IsEmpty)
            {
                var assignment = segment.IndexOf('=');
                var name = assignment < 0 ? segment : segment[..assignment];
                var value = assignment < 0 ? ReadOnlySpan<char>.Empty : segment[(assignment + 1)..];
                pairs.Add(new KeyValuePair<string, string>(Decode(name), Decode(value)));
            }
            if (separator < 0)
            {
                break;
            }
            rawQuery = rawQuery[(separator + 1)..];
        }
        return pairs.ToArray();
    }

    internal static string Serialize(ReadOnlySpan<KeyValuePair<string, string>> pairs)
    {
        var builder = new StringBuilder();
        foreach (var pair in pairs)
        {
            if (builder.Length != 0)
            {
                builder.Append('&');
            }
            AppendEncoded(builder, pair.Key);
            builder.Append('=');
            AppendEncoded(builder, pair.Value);
        }
        return builder.ToString();
    }

    internal static string NormalizeScalarValueString(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsSurrogate(character))
            {
                continue;
            }
            if (char.IsHighSurrogate(character)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                index++;
                continue;
            }
            // UTF-8 replacement fallback gives managed strings the same scalar-value model as
            // decoded query input, keeping builder serialization and parsing symmetric.
            return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value));
        }
        return value;
    }

    private static string Decode(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return string.Empty;
        }
        var byteCount = Encoding.UTF8.GetByteCount(text);
        Span<byte> bytes = byteCount <= 256 ? stackalloc byte[256] : new byte[byteCount];
        var encodedLength = Encoding.UTF8.GetBytes(text, bytes);
        var decodedLength = 0;
        for (var index = 0; index < encodedLength; index++)
        {
            var value = bytes[index];
            if (value == '+')
            {
                bytes[decodedLength++] = (byte)' ';
            }
            else if (value == '%'
                && index + 2 < encodedLength
                && TryReadHexadecimal(bytes[index + 1], out var upper)
                && TryReadHexadecimal(bytes[index + 2], out var lower))
            {
                bytes[decodedLength++] = (byte)((upper << 4) | lower);
                index += 2;
            }
            else
            {
                bytes[decodedLength++] = value;
            }
        }
        return Encoding.UTF8.GetString(bytes[..decodedLength]);
    }

    private static void AppendEncoded(StringBuilder builder, string text)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        Span<byte> bytes = byteCount <= 256 ? stackalloc byte[256] : new byte[byteCount];
        var encodedLength = Encoding.UTF8.GetBytes(text.AsSpan(), bytes);
        foreach (var value in bytes[..encodedLength])
        {
            if (value is >= (byte)'a' and <= (byte)'z'
                or >= (byte)'A' and <= (byte)'Z'
                or >= (byte)'0' and <= (byte)'9'
                or (byte)'*' or (byte)'-' or (byte)'.' or (byte)'_')
            {
                builder.Append((char)value);
            }
            else if (value == ' ')
            {
                builder.Append('+');
            }
            else
            {
                builder.Append('%');
                builder.Append(HexadecimalDigits[value >> 4]);
                builder.Append(HexadecimalDigits[value & 15]);
            }
        }
    }

    private static bool TryReadHexadecimal(byte value, out int digit)
    {
        if (value is >= (byte)'0' and <= (byte)'9')
        {
            digit = value - '0';
            return true;
        }
        if (value is >= (byte)'A' and <= (byte)'F')
        {
            digit = value - 'A' + 10;
            return true;
        }
        if (value is >= (byte)'a' and <= (byte)'f')
        {
            digit = value - 'a' + 10;
            return true;
        }
        digit = 0;
        return false;
    }
}
