using System;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace Assimalign.Viu;

/// <summary>
/// Provides the intentionally public compiler/runtime registration ABI used by generated code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ComponentCompilerServices
{
    private static readonly ConcurrentDictionary<Type, ComponentDevelopmentMetadata> Metadata = new();

    /// <summary>Registers generated development metadata for one component type token.</summary>
    /// <param name="componentType">The generated component type.</param>
    /// <param name="metadata">The compiler-produced metadata.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(
        Type componentType,
        ComponentDevelopmentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata[componentType] = metadata;
    }

    internal static ComponentDevelopmentMetadata? Find(Type componentType) =>
        Metadata.TryGetValue(componentType, out var metadata)
            ? metadata
            : null;
}
