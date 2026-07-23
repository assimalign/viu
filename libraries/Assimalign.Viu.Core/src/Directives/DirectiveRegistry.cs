using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// An immutable, application-owned runtime directive registry with Vue-compatible asset-name
/// lookup.
/// </summary>
/// <remarks>
/// Resolution tries the raw name, its camel-case form, and then its Pascal-case form. See
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/helpers/resolveAssets.ts.
/// Registration keys remain ordinal, so exact duplicate registrations fail while
/// alias-equivalent registrations retain raw-name precedence.
/// </remarks>
public sealed class DirectiveRegistry : IDirectiveResolver
{
    private readonly IReadOnlyDictionary<string, IDirective> _directives;

    /// <summary>Creates a registry from directive name/value pairs.</summary>
    /// <param name="directives">The directive registrations.</param>
    public DirectiveRegistry(IEnumerable<KeyValuePair<string, IDirective>> directives)
    {
        ArgumentNullException.ThrowIfNull(directives);
        Dictionary<string, IDirective> snapshot = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IDirective> registration in directives)
        {
            ArgumentException.ThrowIfNullOrEmpty(registration.Key);
            ArgumentNullException.ThrowIfNull(registration.Value);
            if (!snapshot.TryAdd(registration.Key, registration.Value))
            {
                throw new ArgumentException(
                    $"Directive \"{registration.Key}\" is registered more than once.",
                    nameof(directives));
            }
        }

        _directives = snapshot;
    }

    /// <inheritdoc/>
    public IDirective? Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_directives.TryGetValue(name, out IDirective? directive))
        {
            return directive;
        }

        string camelizedName = Camelize(name);
        if (!string.Equals(camelizedName, name, StringComparison.Ordinal)
            && _directives.TryGetValue(camelizedName, out directive))
        {
            return directive;
        }

        string pascalizedName = Capitalize(camelizedName);
        return !string.Equals(
                   pascalizedName,
                   camelizedName,
                   StringComparison.Ordinal)
               && _directives.TryGetValue(pascalizedName, out directive)
            ? directive
            : null;
    }

    private static string Camelize(string name)
    {
        if (name.IndexOf('-', StringComparison.Ordinal) < 0)
        {
            return name;
        }

        char[] buffer = new char[name.Length];
        int length = 0;
        bool capitalizeNext = false;
        foreach (char character in name)
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

    private static string Capitalize(string name)
    {
        return name.Length == 0
            ? name
            : char.ToUpperInvariant(name[0]) + name[1..];
    }
}
