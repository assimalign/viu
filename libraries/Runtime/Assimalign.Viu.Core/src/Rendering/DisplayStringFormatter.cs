using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assimalign.Viu;

/// <summary>
/// Formats template interpolation values into deterministic client and server text.
/// </summary>
/// <remarks>
/// Null renders empty, scalars use invariant culture, and collections use a JSON-like two-space
/// indented form. A statically typed <see cref="ISet{T}"/> uses the named set convention; after
/// type erasure, an unknown <see cref="IEnumerable"/> uses the ordinary array convention.
/// Formatting never inspects runtime type metadata, serializes members through reflection, or
/// generates code, preserving the WASM/AOT boundary. This public surface is the compiler/runtime
/// ABI specified by <c>[SFC-CG-2]</c> and <c>[V01.01.15.02]</c>.
/// </remarks>
public static class DisplayStringFormatter
{
    /// <summary>Formats one value for text interpolation.</summary>
    /// <param name="value">The value produced by the interpolation expression.</param>
    /// <returns>The deterministic display string, which is never null.</returns>
    public static string ToDisplayString(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (IsJsonShaped(value))
        {
            StringBuilder builder = new();
            WriteJson(builder, value, 0);
            return builder.ToString();
        }

        return FormatScalar(value);
    }

    /// <summary>
    /// Formats a statically typed set with its count and ordered enumeration in the named set
    /// convention.
    /// </summary>
    /// <typeparam name="T">The set element type.</typeparam>
    /// <param name="value">The non-null set to format.</param>
    /// <returns>The deterministic <c>Set(n)</c> display string.</returns>
    /// <remarks>
    /// Generic overload resolution preserves set identity without runtime interface reflection.
    /// A set already erased to <see cref="object"/> is intentionally formatted by the object
    /// overload as an ordinary enumerable. Specified by <c>[SFC-CG-2]</c> and
    /// <c>[V01.01.15.02]</c>.
    /// </remarks>
    public static string ToDisplayString<T>(ISet<T> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder builder = new();
        WriteSetConvention(builder, value, value.Count, 0);
        return builder.ToString();
    }

    /// <summary>
    /// Formats a scalar with invariant culture and lower-case boolean spellings.
    /// </summary>
    /// <param name="value">The non-null scalar value.</param>
    /// <returns>The invariant scalar text, or an empty string when conversion returns null.</returns>
    public static string FormatScalar(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            string text => text,
            bool booleanValue => booleanValue ? "true" : "false",
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static bool IsJsonShaped(object value) =>
        value is IDictionary
            || value is IEnumerable and not string
            || value is IReadOnlyDictionary<string, object?>;

    private static void WriteJson(StringBuilder builder, object? value, int depth)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case string text:
                WriteJsonString(builder, text);
                break;
            case bool booleanValue:
                builder.Append(booleanValue ? "true" : "false");
                break;
            case IReadOnlyDictionary<string, object?> readOnlyMap:
                WriteJsonObject(builder, readOnlyMap, depth);
                break;
            case IDictionary dictionary when HasStringKeys(dictionary):
                WriteJsonObject(builder, EnumeratePairs(dictionary), depth);
                break;
            case IDictionary dictionary:
                WriteMapConvention(builder, dictionary, depth);
                break;
            case IEnumerable enumerable:
                WriteJsonArray(builder, enumerable, depth);
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal:
                builder.Append(FormatScalar(value));
                break;
            default:
                WriteJsonString(builder, FormatScalar(value));
                break;
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumeratePairs(
        IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            yield return new KeyValuePair<string, object?>((string)entry.Key, entry.Value);
        }
    }

    private static bool HasStringKeys(IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string)
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteJsonObject(
        StringBuilder builder,
        IEnumerable<KeyValuePair<string, object?>> pairs,
        int depth)
    {
        builder.Append('{');
        bool isFirst = true;
        foreach (KeyValuePair<string, object?> pair in pairs)
        {
            builder.Append(isFirst ? "\n" : ",\n");
            isFirst = false;
            AppendIndent(builder, depth + 1);
            WriteJsonString(builder, pair.Key);
            builder.Append(": ");
            WriteJson(builder, pair.Value, depth + 1);
        }

        if (!isFirst)
        {
            builder.Append('\n');
            AppendIndent(builder, depth);
        }

        builder.Append('}');
    }

    private static void WriteJsonArray(StringBuilder builder, IEnumerable values, int depth)
    {
        builder.Append('[');
        bool isFirst = true;
        foreach (object? entry in values)
        {
            builder.Append(isFirst ? "\n" : ",\n");
            isFirst = false;
            AppendIndent(builder, depth + 1);
            WriteJson(builder, entry, depth + 1);
        }

        if (!isFirst)
        {
            builder.Append('\n');
            AppendIndent(builder, depth);
        }

        builder.Append(']');
    }

    private static void WriteMapConvention(
        StringBuilder builder,
        IDictionary dictionary,
        int depth)
    {
        builder.Append("{\n");
        AppendIndent(builder, depth + 1);
        WriteJsonString(
            builder,
            $"Map({dictionary.Count.ToString(CultureInfo.InvariantCulture)})");
        builder.Append(": {");
        bool isFirst = true;
        foreach (DictionaryEntry entry in dictionary)
        {
            builder.Append(isFirst ? "\n" : ",\n");
            isFirst = false;
            AppendIndent(builder, depth + 2);
            WriteJsonString(builder, $"{FormatScalar(entry.Key)} =>");
            builder.Append(": ");
            WriteJson(builder, entry.Value, depth + 2);
        }

        if (!isFirst)
        {
            builder.Append('\n');
            AppendIndent(builder, depth + 1);
        }

        builder.Append("}\n");
        AppendIndent(builder, depth);
        builder.Append('}');
    }

    private static void WriteSetConvention(
        StringBuilder builder,
        IEnumerable values,
        int count,
        int depth)
    {
        builder.Append("{\n");
        AppendIndent(builder, depth + 1);
        WriteJsonString(builder, $"Set({count.ToString(CultureInfo.InvariantCulture)})");
        builder.Append(": ");
        WriteJsonArray(builder, values, depth + 1);
        builder.Append('\n');
        AppendIndent(builder, depth);
        builder.Append('}');
    }

    private static void WriteJsonString(StringBuilder builder, string text)
    {
        builder.Append('"');
        foreach (char character in text)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(
                            ((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    private static void AppendIndent(StringBuilder builder, int depth) =>
        builder.Append(' ', depth * 2);
}
