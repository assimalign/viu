using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Identifies an element or attribute without assuming an HTML document model. Identity is the
/// namespace name plus the local name; the prefix is a serialization alias carried for formats
/// that preserve it and deliberately excluded from equality.
/// </summary>
public readonly struct QualifiedName : IEquatable<QualifiedName>
{
    /// <summary>Initializes a qualified name.</summary>
    /// <param name="localName">The non-empty local name.</param>
    /// <param name="namespaceName">The optional namespace name.</param>
    /// <param name="prefix">The optional source prefix, excluded from equality.</param>
    public QualifiedName(string localName, string? namespaceName = null, string? prefix = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(localName);
        LocalName = localName;
        NamespaceName = namespaceName;
        Prefix = prefix;
    }

    /// <summary>Gets the local name.</summary>
    public string LocalName { get; }

    /// <summary>Gets the optional namespace name.</summary>
    public string? NamespaceName { get; }

    /// <summary>Gets the optional source prefix, excluded from equality.</summary>
    public string? Prefix { get; }

    /// <inheritdoc/>
    public bool Equals(QualifiedName other)
        => string.Equals(LocalName, other.LocalName, StringComparison.Ordinal)
            && string.Equals(NamespaceName, other.NamespaceName, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is QualifiedName other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(LocalName, NamespaceName);

    /// <summary>Compares two names by namespace and local name.</summary>
    public static bool operator ==(QualifiedName left, QualifiedName right) => left.Equals(right);

    /// <summary>Compares two names by namespace and local name.</summary>
    public static bool operator !=(QualifiedName left, QualifiedName right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => string.IsNullOrEmpty(Prefix) ? LocalName : string.Concat(Prefix, ":", LocalName);
}
