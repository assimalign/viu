using System;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Provides the culture-invariant name casing used by component, directive, event, and generated-property
/// lookup throughout the Viu runtime.
/// </summary>
/// <remarks>
/// The operations are ordinal and deterministic. Keeping them on one shared surface prevents runtime
/// registries and compiler-generated helper calls from assigning different aliases to the same name.
/// Specified by <c>[V01.01.14.08]</c>.
/// </remarks>
public static class NameNormalization
{
    /// <summary>
    /// Converts a hyphenated name to camel case by removing each hyphen and invariant-capitalizing the
    /// next non-hyphen character.
    /// </summary>
    /// <param name="value">The name to normalize.</param>
    /// <returns>
    /// The camel-case name. A value without a hyphen is returned unchanged and without an allocation.
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

    /// <summary>Invariant-capitalizes the first character of a name.</summary>
    /// <param name="value">The name to normalize.</param>
    /// <returns>The capitalized name, or the original empty string when <paramref name="value"/> is empty.</returns>
    public static string Capitalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
