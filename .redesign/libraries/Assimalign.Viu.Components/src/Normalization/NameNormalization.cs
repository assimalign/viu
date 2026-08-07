using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Invariant name-shape conversions used by contract resolution and registered-name lookup.
/// Moved from the dissolved shared library; the hyphenation form lives here because binding
/// resolution's alias tables need it.
/// </summary>
public static class NameNormalization
{
    /// <summary>Converts a hyphenated or pascal name to camel case.</summary>
    /// <param name="name">The source name.</param>
    /// <returns>The camel-case form.</returns>
    public static string Camelize(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return char.IsUpper(name[0]) ? string.Create(name.Length, name, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            span[0] = char.ToLowerInvariant(span[0]);
        }) : name;
    }

    /// <summary>Converts a camel or hyphenated name to pascal case.</summary>
    /// <param name="name">The source name.</param>
    /// <returns>The pascal-case form.</returns>
    public static string Pascalize(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return char.IsLower(name[0]) ? string.Create(name.Length, name, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            span[0] = char.ToUpperInvariant(span[0]);
        }) : name;
    }

    /// <summary>Converts a camel or pascal name to a lower hyphenated form.</summary>
    /// <param name="name">The source name.</param>
    /// <returns>The hyphenated form.</returns>
    public static string Hyphenate(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        System.Text.StringBuilder builder = new(name.Length + 4);
        foreach (char character in name)
        {
            if (char.IsUpper(character))
            {
                if (builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }
}
