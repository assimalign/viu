namespace Assimalign.Viu;

/// <summary>
/// The node-op that inserts a pre-rendered static markup chunk in one operation and reports the
/// span it produced — the C# port of the <c>insertStaticContent</c> option in
/// <c>@vue/runtime-core</c>'s custom renderer API (https://vuejs.org/api/custom-renderer.html).
/// On the browser this is a single interop call through a detached template ([V01.01.04.01]).
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
/// <param name="content">The raw markup.</param>
/// <param name="parent">The container to insert into.</param>
/// <param name="anchor">The node to insert before, or default to append.</param>
/// <param name="elementNamespace">The namespace (<c>"svg"</c>, <c>"mathml"</c>, or null for HTML).</param>
/// <returns>The first and last nodes of the inserted span, used as patch anchors.</returns>
public delegate (TNode First, TNode Last) InsertStaticContentDelegate<TNode>(
    string content,
    TNode parent,
    TNode? anchor,
    string? elementNamespace);
