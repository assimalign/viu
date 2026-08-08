using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>Provides an immutable application-owned directive registry keyed by type token.</summary>
/// <remarks>
/// The registry performs ordinal type-identity lookup and never activates a type reflectively.
/// Registered instances are borrowed. Specified by <c>[CMP-7]</c>, <c>[APP-2]</c>, and
/// <c>[APP-6]</c>.
/// </remarks>
public sealed class DirectiveRegistry : IDirectiveResolver
{
    private readonly IReadOnlyDictionary<Type, IDirective> _directives;

    /// <summary>Creates a registry from explicit type-token and directive pairs.</summary>
    /// <param name="directives">The directive registrations.</param>
    public DirectiveRegistry(IEnumerable<KeyValuePair<Type, IDirective>> directives)
    {
        ArgumentNullException.ThrowIfNull(directives);
        Dictionary<Type, IDirective> snapshot = [];
        foreach (KeyValuePair<Type, IDirective> registration in directives)
        {
            ArgumentNullException.ThrowIfNull(registration.Key);
            ArgumentNullException.ThrowIfNull(registration.Value);
            if (!snapshot.TryAdd(registration.Key, registration.Value))
            {
                throw new ArgumentException(
                    $"Directive type '{registration.Key}' is registered more than once.",
                    nameof(directives));
            }
        }

        _directives = snapshot;
    }

    /// <inheritdoc/>
    public IDirective? Resolve(Type directiveType)
    {
        ArgumentNullException.ThrowIfNull(directiveType);
        return _directives.TryGetValue(directiveType, out IDirective? directive)
            ? directive
            : null;
    }
}
