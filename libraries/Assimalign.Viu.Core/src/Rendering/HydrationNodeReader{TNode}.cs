namespace Assimalign.Viu;

/// <summary>
/// Reads an immutable or live view of an existing server-rendered host subtree.
/// </summary>
/// <remarks>
/// Hosts may snapshot the complete subtree when the reader is created. This lets a browser or
/// WebView2 host cross its interop boundary once instead of once per structural read. Core reads
/// the complete removal range before mutating it, so immutable snapshots remain valid during
/// mismatch recovery. The reader is not thread-safe.
/// </remarks>
/// <typeparam name="TNode">The host node type.</typeparam>
public abstract class HydrationNodeReader<TNode>
    where TNode : notnull
{
    /// <summary>Initializes a hydration reader.</summary>
    protected HydrationNodeReader()
    {
    }

    /// <summary>Gets the kind of an existing host node.</summary>
    /// <param name="node">The host node.</param>
    /// <returns>The host node kind.</returns>
    public abstract HydrationNodeKind Kind(TNode node);

    /// <summary>Gets the first child, or default when the node has no child.</summary>
    /// <param name="node">The host parent.</param>
    /// <returns>The first child.</returns>
    public abstract TNode? FirstChild(TNode node);

    /// <summary>Gets the next sibling, or default when the node is last.</summary>
    /// <param name="node">The host node.</param>
    /// <returns>The next sibling.</returns>
    public abstract TNode? NextSibling(TNode node);

    /// <summary>Gets the parent, or default when the node is a root.</summary>
    /// <param name="node">The host node.</param>
    /// <returns>The parent.</returns>
    public abstract TNode? ParentNode(TNode node);

    /// <summary>Gets an element's tag name.</summary>
    /// <param name="node">The element node.</param>
    /// <returns>The element tag.</returns>
    public abstract string ElementTag(TNode node);

    /// <summary>Gets a text or comment node's character data.</summary>
    /// <param name="node">The text or comment node.</param>
    /// <returns>The character data.</returns>
    public abstract string Data(TNode node);

    /// <summary>Gets an element attribute, or null when it is absent.</summary>
    /// <param name="node">The element node.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The serialized attribute value.</returns>
    public abstract string? Attribute(TNode node, string name);
}
