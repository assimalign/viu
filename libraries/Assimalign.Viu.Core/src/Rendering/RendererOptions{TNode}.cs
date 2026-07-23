using System;

namespace Assimalign.Viu;

/// <summary>
/// Supplies every platform operation used by <see cref="Renderer{TNode}"/>.
/// </summary>
/// <remarks>
/// This is the host-neutral counterpart of Vue 3.5's custom-renderer options:
/// https://vuejs.org/api/custom-renderer.html. Core never performs browser or WebView2 work
/// directly. A host package supplies these delegates and may batch their effects at its own
/// commit boundary. The renderer is single-threaded and trimming safe.
/// </remarks>
/// <typeparam name="TNode">
/// The platform node type. Hosts using a value-type handle reserve its default value for
/// “no node.”
/// </typeparam>
public sealed class RendererOptions<TNode>
    where TNode : notnull
{
    /// <summary>Inserts or moves a child before an anchor, appending when the anchor is absent.</summary>
    public required Action<TNode, TNode, TNode?> Insert { get; init; }

    /// <summary>Removes a node from its host parent.</summary>
    public required Action<TNode> Remove { get; init; }

    /// <summary>Creates an element for a tag and optional platform namespace.</summary>
    public required Func<string, string?, TNode> CreateElement { get; init; }

    /// <summary>Creates a text node.</summary>
    public required Func<string, TNode> CreateText { get; init; }

    /// <summary>Creates a comment node.</summary>
    public required Func<string, TNode> CreateComment { get; init; }

    /// <summary>Changes the content of an existing text node.</summary>
    public required Action<TNode, string> SetText { get; init; }

    /// <summary>Returns a node's host parent, or default when it has none.</summary>
    public required Func<TNode, TNode?> ParentNode { get; init; }

    /// <summary>Returns a node's next host sibling, or default at the end.</summary>
    public required Func<TNode, TNode?> NextSibling { get; init; }

    /// <summary>Applies one immutable attribute-snapshot difference.</summary>
    public required PatchAttributeDelegate<TNode> PatchAttribute { get; init; }

    /// <summary>
    /// Optionally stamps a compiler-produced scoped-style identifier on an element.
    /// </summary>
    public Action<TNode, string>? SetScopeIdentifier { get; init; }

    /// <summary>
    /// Optionally resolves a teleport target descriptor, such as a CSS selector, to a host
    /// container. A target already assignable to <typeparamref name="TNode"/> is used directly.
    /// </summary>
    public Func<object, TNode?>? ResolveTeleportTarget { get; init; }

    /// <summary>
    /// Optionally commits host mutations accumulated by a buffered adapter. Core invokes it before
    /// post-render callbacks and again when those callbacks enqueue additional host work.
    /// </summary>
    public Action? Commit { get; init; }

    /// <summary>
    /// Optionally inserts a static-content span. Rendering
    /// <see cref="Assimalign.Viu.Components.IStaticComponent"/> requires this operation.
    /// </summary>
    public InsertStaticContentDelegate<TNode>? InsertStaticContent { get; init; }

    /// <summary>
    /// Optionally creates a read-only view over an existing server-rendered subtree. Hydration
    /// requires this operation. A browser or WebView2 host should return a reader backed by one
    /// batched subtree snapshot.
    /// </summary>
    public Func<TNode, HydrationNodeReader<TNode>>? CreateHydrationReader { get; init; }
}
