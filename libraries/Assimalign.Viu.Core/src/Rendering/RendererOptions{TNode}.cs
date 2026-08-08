using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Supplies the complete set of host operations used by <see cref="Renderer{TNode}"/>.
/// </summary>
/// <typeparam name="TNode">
/// The opaque host-node type. Value-handle hosts reserve its default value for no node.
/// </typeparam>
/// <remarks>
/// Core carries no platform handles or markup namespace policy. The host interprets each
/// <see cref="QualifiedName"/> and may buffer work until <see cref="Commit"/>. Specified by
/// <c>[RND-HOST-1]</c> through <c>[RND-HOST-4]</c>.
/// </remarks>
public sealed class RendererOptions<TNode>
    where TNode : notnull
{
    /// <summary>Gets the operation that inserts or moves a child before an optional anchor.</summary>
    public required Action<TNode, TNode, TNode?> Insert { get; init; }

    /// <summary>Gets the operation that removes a host node.</summary>
    public required Action<TNode> Remove { get; init; }

    /// <summary>Gets the operation that creates an element from its complete qualified name.</summary>
    public required Func<QualifiedName, TNode> CreateElement { get; init; }

    /// <summary>Gets the operation that creates a text node.</summary>
    public required Func<string, TNode> CreateText { get; init; }

    /// <summary>Gets the operation that creates a comment node.</summary>
    public required Func<string, TNode> CreateComment { get; init; }

    /// <summary>Gets the operation that changes character data on an existing text node.</summary>
    public required Action<TNode, string> SetText { get; init; }

    /// <summary>Gets the operation that returns a node's parent, or default when detached.</summary>
    public required Func<TNode, TNode?> ParentNode { get; init; }

    /// <summary>Gets the operation that returns a node's next sibling, or default at the end.</summary>
    public required Func<TNode, TNode?> NextSibling { get; init; }

    /// <summary>Gets the operation that applies one immutable binding difference.</summary>
    public required PatchAttributeDelegate<TNode> PatchAttribute { get; init; }

    /// <summary>Gets the optional host target resolver used by teleport nodes.</summary>
    public Func<string, TNode?>? ResolveTeleportTarget { get; init; }

    /// <summary>Gets the optional buffered-host commit operation.</summary>
    public Action? Commit { get; init; }

    /// <summary>Gets the optional one-shot static-content insertion operation.</summary>
    public InsertStaticContentDelegate<TNode>? InsertStaticContent { get; init; }

    /// <summary>Gets the optional existing-subtree reader factory used for hydration.</summary>
    public Func<TNode, HydrationNodeReader<TNode>>? CreateHydrationReader { get; init; }
}
