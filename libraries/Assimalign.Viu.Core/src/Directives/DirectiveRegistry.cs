using System;
using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu;

/// <summary>
/// An immutable, application-owned runtime directive registry with the standard asset-name
/// lookup.
/// </summary>
/// <remarks>
/// Resolution tries the raw name, its camel-case form, and then its Pascal-case form, so a
/// template may spell a directive in the casing that reads naturally at the use site.
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

        string camelizedName = NameNormalization.Camelize(name);
        if (!string.Equals(camelizedName, name, StringComparison.Ordinal)
            && _directives.TryGetValue(camelizedName, out directive))
        {
            return directive;
        }

        string pascalizedName = NameNormalization.Capitalize(camelizedName);
        return !string.Equals(
                   pascalizedName,
                   camelizedName,
                   StringComparison.Ordinal)
               && _directives.TryGetValue(pascalizedName, out directive)
            ? directive
            : null;
    }
}
