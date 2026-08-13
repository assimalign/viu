using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Inserts one compiler-trusted static payload and returns its inclusive host range.
/// </summary>
/// <typeparam name="TNode">The opaque host-node type.</typeparam>
/// <param name="format">The serialization format of the payload.</param>
/// <param name="content">The compiler-trusted serialized content.</param>
/// <param name="parent">The host parent.</param>
/// <param name="anchor">The following host sibling, or default to append.</param>
/// <returns>The inclusive first and last nodes of the inserted range.</returns>
/// <remarks>Specified by <c>[RND-HOST-1]</c> and <c>[RND-HOST-2]</c>.</remarks>
public delegate (TNode First, TNode Last) InsertStaticContentDelegate<TNode>(
    MarkupFormat format,
    string content,
    TNode parent,
    TNode? anchor)
    where TNode : notnull;
