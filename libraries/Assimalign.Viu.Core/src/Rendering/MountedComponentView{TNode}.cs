using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// The public read-only view of one mounted authored component, exposed for cold-path testing
/// and diagnostics queries.
/// </summary>
/// <remarks>
/// The engine caches one view per mounted component, so reference identity is stable across
/// enumerations for the life of the mount. Specified by <c>[RND-6]</c>.
/// </remarks>
/// <typeparam name="TNode">The opaque host node type.</typeparam>
public sealed class MountedComponentView<TNode>
    where TNode : notnull
{
    private readonly MountedComponent<TNode> _mounted;

    internal MountedComponentView(MountedComponent<TNode> mounted)
    {
        ArgumentNullException.ThrowIfNull(mounted);
        _mounted = mounted;
    }

    /// <summary>Gets the immutable component node that created this mount.</summary>
    public ComponentNode Request => (ComponentNode)_mounted.Value;

    /// <summary>Gets the actual authored instance for type-based testing queries.</summary>
    public IComponent Instance => _mounted.Instance;

    /// <summary>Gets the runtime-implemented live context.</summary>
    public ComponentContext Context => _mounted.Context;

    /// <summary>Gets the first current host node, or null for a host-empty subtree.</summary>
    public TNode? FirstHostNode => _mounted.IsUnmounted
        ? default
        : _mounted.Subtree.FirstHostNode;

    /// <summary>Gets the last current host node, or null for a host-empty subtree.</summary>
    public TNode? LastHostNode => _mounted.IsUnmounted
        ? default
        : _mounted.Subtree.LastHostNode;

    /// <summary>Gets whether the component is currently mounted.</summary>
    public bool IsMounted => !_mounted.IsUnmounted;
}
