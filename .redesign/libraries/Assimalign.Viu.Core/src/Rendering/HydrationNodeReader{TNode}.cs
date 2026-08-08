namespace Assimalign.Viu;

/// <summary>Reads an immutable or live view of an existing server-rendered host subtree.</summary>
/// <remarks>
/// A host may snapshot the complete subtree when it creates this reader, allowing every later
/// structural read to remain in managed memory. The reader is not thread-safe. Specified by
/// <c>[HYD-1]</c> and <c>[HYD-2]</c>.
/// </remarks>
/// <typeparam name="TNode">The opaque host node type.</typeparam>
public abstract class HydrationNodeReader<TNode>
    where TNode : notnull
{
    /// <summary>Initializes a host-supplied hydration reader.</summary>
    protected HydrationNodeReader()
    {
    }

    /// <summary>Gets the closed kind of an existing host node.</summary>
    /// <param name="node">The host node.</param>
    /// <returns>The host-neutral node kind.</returns>
    public abstract HydrationNodeKind Kind(TNode node);

    /// <summary>Gets the first child, or the default node value when there is no child.</summary>
    /// <param name="node">The host parent.</param>
    /// <returns>The first child or the missing-node value.</returns>
    public abstract TNode? FirstChild(TNode node);

    /// <summary>Gets the next sibling, or the default node value when this node is last.</summary>
    /// <param name="node">The host node.</param>
    /// <returns>The next sibling or the missing-node value.</returns>
    public abstract TNode? NextSibling(TNode node);

    /// <summary>Gets the parent, or the default node value when this node is a root.</summary>
    /// <param name="node">The host node.</param>
    /// <returns>The parent or the missing-node value.</returns>
    public abstract TNode? ParentNode(TNode node);

    /// <summary>Gets an element node's serialized local tag name.</summary>
    /// <param name="node">The element node.</param>
    /// <returns>The element tag name.</returns>
    public abstract string ElementTag(TNode node);

    /// <summary>Gets a text or comment node's character data.</summary>
    /// <param name="node">The text or comment node.</param>
    /// <returns>The node data.</returns>
    public abstract string Data(TNode node);

    /// <summary>Gets an element attribute, or null when it is absent.</summary>
    /// <param name="node">The element node.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The serialized value or null.</returns>
    public abstract string? Attribute(TNode node, string name);
}
