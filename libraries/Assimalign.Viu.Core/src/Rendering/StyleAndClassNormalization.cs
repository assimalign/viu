using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Collapses accepted class and style binding shapes into deterministic host-facing values.
/// </summary>
/// <remarks>
/// Class bindings accept strings, nested enumerables, and name-to-condition dictionaries. Style
/// bindings accept strings, dictionaries, and nested enumerable combinations. String fast paths
/// do not allocate, and regular expressions are generated at build time for WASM/AOT safety. This
/// public surface is the compiler/runtime ABI specified by <c>[SFC-CG-2]</c>.
/// </remarks>
public static partial class StyleAndClassNormalization
{
    /// <summary>
    /// Normalizes a class binding into an ordered, space-separated string.
    /// </summary>
    /// <param name="value">A string, nested enumerable, condition dictionary, or null.</param>
    /// <returns>
    /// The trimmed class string; dictionary keys contribute only when their values are truthy.
    /// </returns>
    public static string NormalizeClass(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text.Trim();
        }

        StringBuilder builder = new();
        AppendClass(builder, value);
        return builder.ToString().Trim();
    }

    /// <summary>
    /// Normalizes a style binding, merging enumerable entries with later declarations winning.
    /// </summary>
    /// <param name="value">A style string, dictionary, nested enumerable, or null.</param>
    /// <returns>
    /// The original string or dictionary, a merged ordinal dictionary for enumerable input, or
    /// null. String entries inside an enumerable are parsed as CSS declarations.
    /// </returns>
    public static object? NormalizeStyle(object? value)
    {
        if (value is string || value is null)
        {
            return value;
        }

        if (value is IReadOnlyDictionary<string, object?>
            || value is IDictionary<string, object?>)
        {
            return value;
        }

        if (value is IEnumerable enumerable)
        {
            Dictionary<string, object?> merged = new(StringComparer.Ordinal);
            foreach (object? entry in enumerable)
            {
                object? normalized = NormalizeStyle(entry);
                if (normalized is string entryText)
                {
                    foreach (KeyValuePair<string, object?> pair in ParseStringStyle(entryText))
                    {
                        merged[pair.Key] = pair.Value;
                    }
                }
                else if (normalized is IReadOnlyDictionary<string, object?> readOnlyMap)
                {
                    foreach (KeyValuePair<string, object?> pair in readOnlyMap)
                    {
                        merged[pair.Key] = pair.Value;
                    }
                }
                else if (normalized is IDictionary<string, object?> map)
                {
                    foreach (KeyValuePair<string, object?> pair in map)
                    {
                        merged[pair.Key] = pair.Value;
                    }
                }
            }

            return merged;
        }

        return value;
    }

    /// <summary>
    /// Parses inline CSS declarations while preserving semicolons nested in parentheses.
    /// </summary>
    /// <param name="cssText">The non-null inline style text.</param>
    /// <returns>
    /// An ordinal declaration dictionary in source order; comments and incomplete declarations
    /// are omitted.
    /// </returns>
    public static Dictionary<string, object?> ParseStringStyle(string cssText)
    {
        ArgumentNullException.ThrowIfNull(cssText);
        Dictionary<string, object?> declarations = new(StringComparer.Ordinal);
        string withoutComments = StyleCommentPattern().Replace(cssText, string.Empty);
        foreach (string declaration in ListDelimiterPattern().Split(withoutComments))
        {
            if (declaration.Length == 0)
            {
                continue;
            }

            int separatorIndex = declaration.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            string name = declaration[..separatorIndex].Trim();
            string declarationValue = declaration[(separatorIndex + 1)..].Trim();
            if (name.Length > 0 && declarationValue.Length > 0)
            {
                declarations[name] = declarationValue;
            }
        }

        return declarations;
    }

    /// <summary>
    /// Serializes a normalized style value into deterministic inline CSS text.
    /// </summary>
    /// <param name="style">A style string, string-keyed dictionary, or null.</param>
    /// <returns>
    /// The original string, serialized declarations with camel-case names hyphenated, or an empty
    /// string for null and unsupported values.
    /// </returns>
    public static string StringifyStyle(object? style)
    {
        if (style is null)
        {
            return string.Empty;
        }

        if (style is string text)
        {
            return text;
        }

        StringBuilder builder = new();
        if (style is IReadOnlyDictionary<string, object?> readOnlyMap)
        {
            foreach (KeyValuePair<string, object?> pair in readOnlyMap)
            {
                AppendStyleDeclaration(builder, pair.Key, pair.Value);
            }
        }
        else if (style is IDictionary<string, object?> map)
        {
            foreach (KeyValuePair<string, object?> pair in map)
            {
                AppendStyleDeclaration(builder, pair.Key, pair.Value);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Applies host binding truthiness: false, null, numeric zero, NaN, and empty strings are
    /// false; every other value is true.
    /// </summary>
    /// <param name="value">The binding value to test.</param>
    /// <returns>Whether the value contributes its conditional class name.</returns>
    public static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool booleanValue => booleanValue,
        string text => text.Length > 0,
        sbyte number => number != 0,
        byte number => number != 0,
        short number => number != 0,
        ushort number => number != 0,
        int number => number != 0,
        uint number => number != 0,
        long number => number != 0,
        ulong number => number != 0,
        float number => number != 0 && !float.IsNaN(number),
        double number => number != 0 && !double.IsNaN(number),
        decimal number => number != 0,
        _ => true,
    };

    private static void AppendClass(StringBuilder builder, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string text)
        {
            AppendClassToken(builder, text);
            return;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyMap)
        {
            foreach (KeyValuePair<string, object?> pair in readOnlyMap)
            {
                if (IsTruthy(pair.Value))
                {
                    AppendClassToken(builder, pair.Key);
                }
            }

            return;
        }

        if (value is IDictionary<string, object?> map)
        {
            foreach (KeyValuePair<string, object?> pair in map)
            {
                if (IsTruthy(pair.Value))
                {
                    AppendClassToken(builder, pair.Key);
                }
            }

            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? entry in enumerable)
            {
                AppendClass(builder, entry);
            }

            return;
        }

        AppendClassToken(builder, value.ToString());
    }

    private static void AppendClassToken(StringBuilder builder, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(token.Trim());
    }

    private static void AppendStyleDeclaration(
        StringBuilder builder,
        string name,
        object? value)
    {
        if (value is null || name.Length == 0)
        {
            return;
        }

        string propertyName = name.StartsWith("--", StringComparison.Ordinal)
            ? name
            : NameNormalization.Hyphenate(name);
        builder.Append(propertyName);
        builder.Append(':');
        builder.Append(DisplayStringFormatter.FormatScalar(value));
        builder.Append(';');
    }

    [GeneratedRegex(@";(?![^(]*\))")]
    private static partial Regex ListDelimiterPattern();

    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex StyleCommentPattern();
}
