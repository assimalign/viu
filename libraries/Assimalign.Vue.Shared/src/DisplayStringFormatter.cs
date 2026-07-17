using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assimalign.Vue.Shared;

/// <summary>
/// Text-interpolation formatting — the C# port of <c>toDisplayString</c> from
/// <c>@vue/shared</c> (<c>packages/shared/src/toDisplayString.ts</c>,
/// https://vuejs.org/guide/essentials/template-syntax.html). Null renders empty, scalars via
/// invariant culture (mandatory so SSR output and client hydration agree), and collections
/// render in upstream's JSON-like two-space-indented shape, including its Map/Set replacer
/// conventions for non-string-keyed dictionaries and sets. Hand-written recursion over
/// <see cref="IDictionary"/>/<see cref="IEnumerable"/> — no reflection-based serialization
/// (WASM AOT/trimming constraint).
/// </summary>
public static class DisplayStringFormatter
{
    /// <summary>Formats <paramref name="value"/> for text interpolation (upstream: <c>toDisplayString</c>).</summary>
    /// <param name="value">The interpolated value.</param>
    /// <returns>The display string; never null.</returns>
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
            var builder = new StringBuilder();
            WriteJson(builder, value, 0);
            return builder.ToString();
        }
        return FormatScalar(value);
    }

    /// <summary>
    /// Scalar formatting with invariant culture: JavaScript-style booleans
    /// (<c>true</c>/<c>false</c>), <see cref="IFormattable"/> via
    /// <see cref="CultureInfo.InvariantCulture"/>, everything else via <c>ToString()</c>.
    /// </summary>
    /// <param name="value">The scalar value.</param>
    public static string FormatScalar(object value) => value switch
    {
        string text => text,
        bool boolValue => boolValue ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    private static bool IsJsonShaped(object value)
        => value is IDictionary
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
            case bool boolValue:
                builder.Append(boolValue ? "true" : "false");
                break;
            case IReadOnlyDictionary<string, object?> readOnlyMap:
                WriteJsonObject(builder, EnumeratePairs(readOnlyMap), depth);
                break;
            case IDictionary dictionary when HasStringKeys(dictionary):
                WriteJsonObject(builder, EnumeratePairs(dictionary), depth);
                break;
            case IDictionary dictionary:
                // Upstream Map replacer: { "Map(n)": { "key =>": value } }.
                WriteMapConvention(builder, dictionary, depth);
                break;
            case IEnumerable enumerable when IsSetLike(value):
                // Upstream Set replacer: { "Set(n)": [ ... ] }.
                WriteSetConvention(builder, enumerable, depth);
                break;
            case IEnumerable enumerable:
                WriteJsonArray(builder, enumerable, depth);
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                builder.Append(FormatScalar(value));
                break;
            default:
                WriteJsonString(builder, FormatScalar(value));
                break;
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumeratePairs(IReadOnlyDictionary<string, object?> map)
        => map;

    private static IEnumerable<KeyValuePair<string, object?>> EnumeratePairs(IDictionary dictionary)
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

    // ISet<T> has no non-generic or covariant view, so set-ness is detected by an interface
    // test on the live instance's type, cached per type. No member is ever read reflectively
    // (the recursion serializes only through IDictionary/IEnumerable dispatch).
    private static readonly Dictionary<Type, bool> SetLikeCache = [];

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:UnrecognizedReflectionPattern",
        Justification = "Selects a display convention only: if the trimmer removes an unused "
            + "ISet<T> implementation from a type's interface list, the value degrades to the "
            + "array display shape - output cosmetics, never a correctness or activation path.")]
    private static bool IsSetLike(object value)
    {
        var type = value.GetType();
        if (SetLikeCache.TryGetValue(type, out var isSetLike))
        {
            return isSetLike;
        }
        isSetLike = false;
        foreach (var candidate in type.GetInterfaces())
        {
            if (candidate.IsGenericType)
            {
                var definition = candidate.GetGenericTypeDefinition();
                if (definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>))
                {
                    isSetLike = true;
                    break;
                }
            }
        }
        SetLikeCache[type] = isSetLike;
        return isSetLike;
    }

    private static void WriteJsonObject(StringBuilder builder, IEnumerable<KeyValuePair<string, object?>> pairs, int depth)
    {
        builder.Append('{');
        var first = true;
        foreach (var (name, entryValue) in pairs)
        {
            builder.Append(first ? "\n" : ",\n");
            first = false;
            AppendIndent(builder, depth + 1);
            WriteJsonString(builder, name);
            builder.Append(": ");
            WriteJson(builder, entryValue, depth + 1);
        }
        if (!first)
        {
            builder.Append('\n');
            AppendIndent(builder, depth);
        }
        builder.Append('}');
    }

    private static void WriteJsonArray(StringBuilder builder, IEnumerable values, int depth)
    {
        builder.Append('[');
        var first = true;
        foreach (var entry in values)
        {
            builder.Append(first ? "\n" : ",\n");
            first = false;
            AppendIndent(builder, depth + 1);
            WriteJson(builder, entry, depth + 1);
        }
        if (!first)
        {
            builder.Append('\n');
            AppendIndent(builder, depth);
        }
        builder.Append(']');
    }

    private static void WriteMapConvention(StringBuilder builder, IDictionary dictionary, int depth)
    {
        builder.Append("{\n");
        AppendIndent(builder, depth + 1);
        WriteJsonString(builder, $"Map({dictionary.Count.ToString(CultureInfo.InvariantCulture)})");
        builder.Append(": {");
        var first = true;
        foreach (DictionaryEntry entry in dictionary)
        {
            builder.Append(first ? "\n" : ",\n");
            first = false;
            AppendIndent(builder, depth + 2);
            WriteJsonString(builder, $"{FormatScalar(entry.Key)} =>");
            builder.Append(": ");
            WriteJson(builder, entry.Value, depth + 2);
        }
        if (!first)
        {
            builder.Append('\n');
            AppendIndent(builder, depth + 1);
        }
        builder.Append("}\n");
        AppendIndent(builder, depth);
        builder.Append('}');
    }

    private static void WriteSetConvention(StringBuilder builder, IEnumerable values, int depth)
    {
        var count = 0;
        foreach (var _ in values)
        {
            count++;
        }
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
        foreach (var character in text)
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
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
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

    private static void AppendIndent(StringBuilder builder, int depth)
        => builder.Append(' ', depth * 2);
}
