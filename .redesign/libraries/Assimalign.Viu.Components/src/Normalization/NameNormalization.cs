using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Provides the ordinal, culture-invariant name conversions shared by component contract
/// resolution and registered-name lookup. Specified by <c>[CMP-6]</c> and <c>[CMP-13]</c>.
/// </summary>
public static class NameNormalization
{
    /// <summary>
    /// Converts a hyphenated name to camel case by removing each hyphen and
    /// invariant-capitalizing the next non-hyphen character.
    /// </summary>
    /// <param name="value">The name to normalize.</param>
    /// <returns>
    /// The camel-case name. A value without a hyphen is returned unchanged and without an
    /// allocation.
    /// </returns>
    public static string Camelize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IndexOf('-', StringComparison.Ordinal) < 0)
        {
            return value;
        }

        char[] buffer = new char[value.Length];
        int length = 0;
        bool capitalizeNext = false;
        foreach (char character in value)
        {
            if (character == '-')
            {
                capitalizeNext = true;
                continue;
            }

            buffer[length] = capitalizeNext
                ? char.ToUpperInvariant(character)
                : character;
            length++;
            capitalizeNext = false;
        }

        return new string(buffer, 0, length);
    }

    /// <summary>Converts a hyphenated or camel-case name to Pascal case.</summary>
    /// <param name="value">The name to normalize.</param>
    /// <returns>The Pascal-case name, or the original empty string.</returns>
    public static string Pascalize(string value)
    {
        string camelized = Camelize(value);
        if (camelized.Length == 0)
        {
            return camelized;
        }

        char firstCharacter = char.ToUpperInvariant(camelized[0]);
        if (firstCharacter == camelized[0])
        {
            return camelized;
        }

        return string.Create(
            camelized.Length,
            (Source: camelized, FirstCharacter: firstCharacter),
            static (span, state) =>
        {
            state.Source.AsSpan().CopyTo(span);
            span[0] = state.FirstCharacter;
        });
    }

    /// <summary>
    /// Converts camel case to lower hyphenated form. A leading capital is lower-cased without a
    /// leading hyphen, preserving vendor-prefixed names.
    /// </summary>
    /// <param name="name">The name to normalize.</param>
    /// <returns>The lower hyphenated form.</returns>
    public static string Hyphenate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int hyphenCount = 0;
        for (int index = 1; index < name.Length; index++)
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
            int position = 0;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
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
}
