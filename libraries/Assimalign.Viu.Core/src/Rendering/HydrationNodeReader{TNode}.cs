namespace Assimalign.Viu;

/// <summary>
/// The read-only view of the existing server-rendered node tree the hydration walker adopts —
/// the C# analog of the <c>nodeType</c>/<c>data</c>/<c>tagName</c>/<c>firstChild</c>/<c>nextSibling</c>
/// reads <c>@vue/runtime-core</c>'s hydration functions perform directly on DOM <c>Node</c>s
/// (<c>packages/runtime-core/src/hydration.ts</c>, https://vuejs.org/guide/scaling-up/ssr.html#client-hydration).
/// The walker never touches a platform node except through these reads and the write-side
/// <see cref="RendererOptions{TNode}"/> ops, so hydration stays platform-agnostic: tests supply a
/// reader over the in-memory tree, and the browser supplies one backed by a single batched
/// interop snapshot of the container subtree (so a whole subtree crosses the JS boundary once
/// rather than a marshaled call per <c>nextSibling</c>/<c>getAttribute</c>).
/// <para>
/// A reader is created per hydration root through
/// <see cref="RendererOptions{TNode}.CreateHydrationReader"/> and is valid for the nodes reachable
/// from that root; it answers reads without further platform round-trips. Not thread-safe
/// (single-threaded JS event-loop model).
/// </para>
/// </summary>
/// <typeparam name="TNode">The platform node type; <c>default</c> means "no node".</typeparam>
public abstract class HydrationNodeReader<TNode>
    where TNode : notnull
{
    /// <summary>Initializes the base reader.</summary>
    protected HydrationNodeReader()
    {
    }

    /// <summary>
    /// Reports what kind of node <paramref name="node"/> is (upstream: the <c>node.nodeType</c>
    /// checks against <c>DOMNodeTypes</c>).
    /// </summary>
    /// <param name="node">The node to classify.</param>
    /// <returns>The node's kind.</returns>
    public abstract HydrationNodeKind Kind(TNode node);

    /// <summary>
    /// Returns <paramref name="node"/>'s first child, or <c>default</c> when it has none (upstream:
    /// <c>node.firstChild</c>). Called to descend into an adopted element's children.
    /// </summary>
    /// <param name="node">The parent node.</param>
    /// <returns>The first child, or <c>default</c>.</returns>
    public abstract TNode? FirstChild(TNode node);

    /// <summary>
    /// Returns <paramref name="node"/>'s next sibling, or <c>default</c> at the end (upstream:
    /// <c>nextSibling(node)</c>). Called to advance the walk to the node after the one just adopted.
    /// </summary>
    /// <param name="node">The node whose successor is requested.</param>
    /// <returns>The next sibling, or <c>default</c>.</returns>
    public abstract TNode? NextSibling(TNode node);

    /// <summary>
    /// Returns <paramref name="node"/>'s parent, or <c>default</c> at a root (upstream:
    /// <c>parentNode(node)</c>). Used to resolve the container a fragment's or component's children
    /// mount into and to reparent a mismatched subtree.
    /// </summary>
    /// <param name="node">The node whose parent is requested.</param>
    /// <returns>The parent, or <c>default</c>.</returns>
    public abstract TNode? ParentNode(TNode node);

    /// <summary>
    /// Returns an element node's tag name (upstream: <c>(node as Element).tagName</c>). Compared
    /// case-insensitively against the vnode's <see cref="VirtualNode.ElementTag"/> to decide whether
    /// the element matches. Only called when <see cref="Kind"/> is
    /// <see cref="HydrationNodeKind.Element"/>.
    /// </summary>
    /// <param name="node">The element node.</param>
    /// <returns>The element's tag name.</returns>
    public abstract string ElementTag(TNode node);

    /// <summary>
    /// Returns a text or comment node's character content (upstream: <c>(node as Text).data</c> /
    /// <c>(node as Comment).data</c>). For a comment this is the content between <c>&lt;!--</c> and
    /// <c>--&gt;</c>, so the fragment markers <c>[</c>/<c>]</c> and the teleport markers are matched
    /// on this value. Only called when <see cref="Kind"/> is
    /// <see cref="HydrationNodeKind.Text"/> or <see cref="HydrationNodeKind.Comment"/>.
    /// </summary>
    /// <param name="node">The text or comment node.</param>
    /// <returns>The node's character content.</returns>
    public abstract string Data(TNode node);

    /// <summary>
    /// Returns an element's attribute value, or null when the attribute is absent (upstream:
    /// <c>el.getAttribute(name)</c>). Used only for dev-mode mismatch reporting — locating the
    /// <c>data-allow-mismatch</c> escape hatch and comparing server-rendered <c>class</c>/<c>style</c>/
    /// attribute values against the client vnode. Only called when <see cref="Kind"/> is
    /// <see cref="HydrationNodeKind.Element"/>.
    /// </summary>
    /// <param name="node">The element node.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The attribute value, or null when absent.</returns>
    public abstract string? Attribute(TNode node, string name);
}
