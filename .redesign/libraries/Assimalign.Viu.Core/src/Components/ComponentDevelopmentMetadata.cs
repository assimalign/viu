using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Assimalign.Viu;

/// <summary>
/// Carries compiler-generated development identities used by hot reload without reflective discovery.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ComponentDevelopmentMetadata
{
    /// <summary>Initializes compiler-generated development metadata.</summary>
    /// <param name="componentIdentifier">The stable generated component identifier.</param>
    /// <param name="generatedTypes">Generated marker types associated with the component.</param>
    public ComponentDevelopmentMetadata(
        string componentIdentifier,
        IEnumerable<Type>? generatedTypes = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentIdentifier);
        ComponentIdentifier = componentIdentifier;
        GeneratedTypes = generatedTypes is null
            ? Array.Empty<Type>()
            : new List<Type>(generatedTypes).AsReadOnly();
    }

    /// <summary>Gets the stable generated component identifier.</summary>
    public string ComponentIdentifier { get; }

    /// <summary>Gets the immutable generated marker type snapshot.</summary>
    public IReadOnlyList<Type> GeneratedTypes { get; }
}
