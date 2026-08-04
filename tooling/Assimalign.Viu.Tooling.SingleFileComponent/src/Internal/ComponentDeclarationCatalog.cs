using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Tooling.SingleFileComponent;

/// <summary>
/// The set of component declarations a compilation can read at build time, keyed for the same name
/// resolution the runtime factory performs [CMP-6]: the raw name, then its camel-case spelling, then the
/// Pascal-case spelling of that, so a template's <c>my-widget</c> finds a <c>MyWidget</c> component.
/// Value-equatable, so it can join a cached per-file projection without defeating the cache.
/// <para>
/// A name that resolves to <b>more than one</b> declaration is deliberately dropped: two same-named
/// components in different namespaces make the template's intent ambiguous, and validating against the
/// wrong one would be a false positive ([SFC-USE-5]).
/// </para>
/// </summary>
internal readonly struct ComponentDeclarationCatalog : IEquatable<ComponentDeclarationCatalog>
{
    /// <summary>The catalog of a compilation that declares no statically readable component.</summary>
    public static readonly ComponentDeclarationCatalog Empty =
        new(EquatableArray<ComponentDeclarationEntry>.Empty);

    private readonly Dictionary<string, ComponentDeclarationEntry?>? _byName;

    /// <summary>Builds the catalog over <paramref name="entries"/>.</summary>
    /// <param name="entries">The declarations, in a stable order.</param>
    public ComponentDeclarationCatalog(EquatableArray<ComponentDeclarationEntry> entries)
    {
        Entries = entries;
        if (entries.Count == 0)
        {
            _byName = null;
            return;
        }

        // A null value marks an ambiguous name: two declarations answer to it, so neither is used.
        _byName = new Dictionary<string, ComponentDeclarationEntry?>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            // A repeated name is ambiguous, full stop. Two components can only both answer to one tag if
            // the application's own registration picks between them, which the compiler cannot see.
            _byName[entry.Name] = _byName.ContainsKey(entry.Name) ? null : entry;
        }
    }

    /// <summary>The declarations this catalog carries, in a stable order.</summary>
    public EquatableArray<ComponentDeclarationEntry> Entries { get; }

    /// <summary>Whether the catalog carries no declaration.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Resolves <paramref name="tag"/> through the [CMP-6] name ladder, returning false when the tag
    /// names no statically readable component or names more than one.
    /// </summary>
    /// <param name="tag">The component tag as authored in the template.</param>
    /// <param name="entry">The resolved declaration.</param>
    /// <returns>True when exactly one declaration answers to the tag.</returns>
    public bool TryResolve(string tag, out ComponentDeclarationEntry entry)
    {
        entry = default;
        if (_byName is null)
        {
            return false;
        }

        foreach (var candidate in NameLadder(tag))
        {
            if (!_byName.TryGetValue(candidate, out var resolved))
            {
                continue;
            }

            if (resolved is not { } declaration)
            {
                return false; // ambiguous: stay silent rather than pick one.
            }

            entry = declaration;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool Equals(ComponentDeclarationCatalog other) => Entries.Equals(other.Entries);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is ComponentDeclarationCatalog other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Entries.GetHashCode();

    private static IEnumerable<string> NameLadder(string tag)
    {
        yield return tag;
        var camelized = ComponentDeclarationNames.Canonicalize(tag);
        if (!string.Equals(camelized, tag, StringComparison.Ordinal))
        {
            yield return camelized;
        }

        if (camelized.Length > 0 && char.IsLower(camelized[0]))
        {
            yield return char.ToUpperInvariant(camelized[0]) + camelized.Substring(1);
        }
    }
}
