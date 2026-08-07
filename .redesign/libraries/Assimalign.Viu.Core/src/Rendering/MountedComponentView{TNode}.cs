using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// The public read-only view of one mounted authored component, exposed for cold-path testing
/// and diagnostics queries.
/// </summary>
/// <remarks>
/// The engine caches one view per mounted component, so reference identity is stable across
/// enumerations for the life of the mount.
/// </remarks>
/// <typeparam name="TNode">The opaque host node type.</typeparam>
public sealed class MountedComponentView<TNode>
    where TNode : class
{
    /// <summary>Initializes an engine-owned mounted-component view.</summary>
    /// <param name="request">The component node that created this mount.</param>
    /// <param name="instance">The activated authored instance.</param>
    /// <param name="context">The runtime-implemented live context.</param>
    /// <param name="firstHostNode">The first current host node, or null for a host-empty subtree.</param>
    /// <param name="lastHostNode">The last current host node, or null for a host-empty subtree.</param>
    /// <param name="isMounted">Whether the component is currently mounted.</param>
    public MountedComponentView(
        ComponentNode request,
        IComponent instance,
        ComponentContext context,
        TNode? firstHostNode,
        TNode? lastHostNode,
        bool isMounted)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(context);
        Request = request;
        Instance = instance;
        Context = context;
        FirstHostNode = firstHostNode;
        LastHostNode = lastHostNode;
        IsMounted = isMounted;
    }

    /// <summary>Gets the immutable component node that created this mount.</summary>
    public ComponentNode Request { get; }

    /// <summary>Gets the actual authored instance for type-based testing queries.</summary>
    public IComponent Instance { get; }

    /// <summary>Gets the runtime-implemented live context.</summary>
    public ComponentContext Context { get; }

    /// <summary>Gets the first current host node, or null for a host-empty subtree.</summary>
    public TNode? FirstHostNode { get; }

    /// <summary>Gets the last current host node, or null for a host-empty subtree.</summary>
    public TNode? LastHostNode { get; }

    /// <summary>Gets whether the component is currently mounted.</summary>
    public bool IsMounted { get; }
}
