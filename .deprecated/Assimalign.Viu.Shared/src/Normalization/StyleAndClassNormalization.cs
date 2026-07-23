using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Class and style value normalization — the C# port of <c>normalizeClass</c>,
/// <c>normalizeStyle</c>, <c>parseStringStyle</c>, and <c>stringifyStyle</c> from
/// <c>@vue/shared</c> (<c>packages/shared/src/normalizeProp.ts</c>,
/// https://vuejs.org/guide/essentials/class-and-style.html). Bindings accept string, nested
/// enumerable, and dictionary (name → truthy) forms exactly as Vue 3 does; truthiness follows
/// JavaScript semantics (false, null, numeric zero, NaN, and "" are falsy). These run per vnode
/// on the patch hot path — string inputs take allocation-free fast paths.
/// </summary>
public static partial class StyleAndClassNormalization
{
    /// <summary>
    /// Normalizes a class binding to its space-joined string form (upstream:
    /// <c>normalizeClass</c>). Strings pass through trimmed; enumerables recurse; dictionaries
    /// contribute the keys whose values are truthy, in entry order.
    /// </summary>
    /// <param name="value">The class binding: string, enumerable, dictionary, or null.</param>
    /// <returns>The normalized class string (possibly empty).</returns>
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
        var builder = new StringBuilder();
        AppendClass(builder, value);
        return builder.ToString().Trim();
    }

    /// <summary>
    /// Normalizes a style binding (upstream: <c>normalizeStyle</c>): enumerables merge into one
    /// dictionary with later entries winning (string entries parse via
    /// <see cref="ParseStringStyle"/>); strings and dictionaries pass through.
    /// </summary>
    /// <param name="value">The style binding: string, dictionary, enumerable of either, or null.</param>
    /// <returns>A string, a merged dictionary, or null.</returns>
    public static object? NormalizeStyle(object? value)
    {
        if (value is string || value is null)
        {
            return value;
        }
        if (value is IReadOnlyDictionary<string, object?> || value is IDictionary<string, object?>)
        {
            return value;
        }
        if (value is IEnumerable enumerable)
        {
            var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var entry in enumerable)
            {
                var normalized = NormalizeStyle(entry);
                if (normalized is string entryText)
                {
                    foreach (var (name, entryValue) in ParseStringStyle(entryText))
                    {
                        merged[name] = entryValue;
                    }
                }
                else if (normalized is IReadOnlyDictionary<string, object?> readOnlyMap)
                {
                    foreach (var (name, entryValue) in readOnlyMap)
                    {
                        merged[name] = entryValue;
                    }
                }
                else if (normalized is IDictionary<string, object?> map)
                {
                    foreach (var pair in map)
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
    /// Parses a CSS declaration string into a name → value dictionary (upstream:
    /// <c>parseStringStyle</c>): comments are stripped, declarations split on <c>;</c> except
    /// inside parentheses (so <c>url(data:...)</c> survives), and each declaration splits on
    /// its first <c>:</c>.
    /// </summary>
    /// <param name="cssText">The inline style text.</param>
    /// <returns>The parsed declarations, in source order.</returns>
    public static Dictionary<string, object?> ParseStringStyle(string cssText)
    {
        ArgumentNullException.ThrowIfNull(cssText);
        var declarations = new Dictionary<string, object?>(StringComparer.Ordinal);
        var withoutComments = StyleCommentPattern().Replace(cssText, string.Empty);
        foreach (var declaration in ListDelimiterPattern().Split(withoutComments))
        {
            if (declaration.Length == 0)
            {
                continue;
            }
            var separatorIndex = declaration.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }
            var name = declaration[..separatorIndex].Trim();
            var declarationValue = declaration[(separatorIndex + 1)..].Trim();
            if (name.Length > 0 && declarationValue.Length > 0)
            {
                declarations[name] = declarationValue;
            }
        }
        return declarations;
    }

    /// <summary>
    /// Serializes a style value to inline CSS text (upstream: <c>stringifyStyle</c>): strings
    /// pass through; dictionary keys emit as-is when kebab-case or <c>--custom</c>, camelCase
    /// keys hyphenate.
    /// </summary>
    /// <param name="style">The style value: string, dictionary, or null.</param>
    /// <returns>The CSS text (possibly empty).</returns>
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
        var builder = new StringBuilder();
        if (style is IReadOnlyDictionary<string, object?> readOnlyMap)
        {
            foreach (var (name, value) in readOnlyMap)
            {
                AppendStyleDeclaration(builder, name, value);
            }
        }
        else if (style is IDictionary<string, object?> map)
        {
            foreach (var pair in map)
            {
                AppendStyleDeclaration(builder, pair.Key, pair.Value);
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// JavaScript truthiness for binding values (upstream's implicit coercion): false, null,
    /// numeric zero, NaN, and the empty string are falsy; everything else is truthy.
    /// </summary>
    /// <param name="value">The value to test.</param>
    public static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool boolValue => boolValue,
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
            foreach (var (name, condition) in readOnlyMap)
            {
                if (IsTruthy(condition))
                {
                    AppendClassToken(builder, name);
                }
            }
            return;
        }
        if (value is IDictionary<string, object?> map)
        {
            foreach (var pair in map)
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
            foreach (var entry in enumerable)
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

    private static void AppendStyleDeclaration(StringBuilder builder, string name, object? value)
    {
        if (value is null || name.Length == 0)
        {
            return;
        }
        var propertyName = name.StartsWith("--", StringComparison.Ordinal) ? name : Hyphenate(name);
        builder.Append(propertyName).Append(':').Append(DisplayStringFormatter.FormatScalar(value)).Append(';');
    }

    /// <summary>
    /// Converts camelCase to kebab-case (upstream: <c>hyphenate</c>, whose <c>\B([A-Z])</c>
    /// inserts no hyphen before a leading capital: <c>WebkitTransition</c> →
    /// <c>webkit-transition</c>).
    /// </summary>
    /// <param name="name">The camelCase name.</param>
    public static string Hyphenate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var hyphenCount = 0;
        for (var index = 1; index < name.Length; index++)
        {
            if (char.IsAsciiLetterUpper(name[index]))
            {
                hyphenCount++;
            }
        }
        if (hyphenCount == 0 && (name.Length == 0 || !char.IsAsciiLetterUpper(name[0])))
        {
            return name;
        }
        return string.Create(name.Length + hyphenCount, name, static (span, source) =>
        {
            var position = 0;
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                if (char.IsAsciiLetterUpper(character))
                {
                    if (index > 0)
                    {
                        span[position++] = '-';
                    }
                    span[position++] = char.ToLowerInvariant(character);
                }
                else
                {
                    span[position++] = character;
                }
            }
        });
    }

    // Upstream listDelimiterRE: split on ';' not inside parentheses.
    [GeneratedRegex(@";(?![^(]*\))")]
    private static partial Regex ListDelimiterPattern();

    // Upstream styleCommentRE: strip /* ... */ comments (JS [^] becomes [\s\S] in .NET).
    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex StyleCommentPattern();
}
