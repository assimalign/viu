namespace Assimalign.Viu;

/// <summary>
/// Inserts one platform-specific static-content span and reports its inclusive host range.
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
/// <param name="content">The static content.</param>
/// <param name="parent">The host parent.</param>
/// <param name="anchor">The host node before which the span is inserted, or default to append.</param>
/// <param name="elementNamespace">The current platform namespace.</param>
/// <returns>The first and last nodes in the inserted span.</returns>
public delegate (TNode First, TNode Last) InsertStaticContentDelegate<TNode>(
    string content,
    TNode parent,
    TNode? anchor,
    string? elementNamespace);
