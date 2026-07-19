using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The platform node-ops a <see cref="Renderer{TNode}"/> is built over — the C# port of
/// <c>RendererOptions</c> in <c>@vue/runtime-core</c>'s custom renderer API
/// (https://vuejs.org/api/custom-renderer.html, <c>packages/runtime-core/src/renderer.ts</c>).
/// The renderer never touches a platform node except through these delegates: the browser
/// supplies JS-interop ops ([V01.01.04.01]), tests supply the in-memory tree
/// ([V01.01.11.01]), and SSR supplies a string builder. Every op call may be a marshaled
/// interop call on WASM, so the pipeline treats each invocation as costly.
/// <para>
/// For value-type <typeparamref name="TNode"/> (e.g. an <see cref="int"/> interop handle),
/// <c>default(TNode)</c> stands in for "no node" anywhere an anchor or parent is optional — the
/// platform must never issue <c>default</c> as a real node.
/// </para>
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
public sealed class RendererOptions<TNode>
{
    /// <summary>
    /// Inserts <c>child</c> into <c>parent</c> before <c>anchor</c>, appending when the anchor
    /// is default (upstream: <c>insert(el, parent, anchor)</c>).
    /// </summary>
    public required Action<TNode, TNode, TNode?> Insert { get; init; }

    /// <summary>Removes the node from its parent (upstream: <c>remove(el)</c>).</summary>
    public required Action<TNode> Remove { get; init; }

    /// <summary>
    /// Creates an element from a tag and namespace (<c>"svg"</c>, <c>"mathml"</c>, or null for
    /// HTML) (upstream: <c>createElement(type, namespace)</c>).
    /// </summary>
    public required Func<string, string?, TNode> CreateElement { get; init; }

    /// <summary>Creates a text node (upstream: <c>createText</c>).</summary>
    public required Func<string, TNode> CreateText { get; init; }

    /// <summary>Creates a comment node (upstream: <c>createComment</c>).</summary>
    public required Func<string, TNode> CreateComment { get; init; }

    /// <summary>Sets a text node's content (upstream: <c>setText</c>).</summary>
    public required Action<TNode, string> SetText { get; init; }

    /// <summary>
    /// Replaces an element's entire content with a text string (upstream:
    /// <c>setElementText</c>).
    /// </summary>
    public required Action<TNode, string> SetElementText { get; init; }

    /// <summary>Returns a node's parent, or default at a root (upstream: <c>parentNode</c>).</summary>
    public required Func<TNode, TNode?> ParentNode { get; init; }

    /// <summary>Returns a node's next sibling, or default at the end (upstream: <c>nextSibling</c>).</summary>
    public required Func<TNode, TNode?> NextSibling { get; init; }

    /// <summary>Lands one prop change on an element (upstream: <c>patchProp</c>).</summary>
    public required PatchPropertyDelegate<TNode> PatchProperty { get; init; }

    /// <summary>Optional: resolves a selector to a node (upstream: <c>querySelector</c>).</summary>
    public Func<string, TNode?>? QuerySelector { get; init; }

    /// <summary>
    /// Optional: stamps a scoped-style id attribute on an element (upstream: <c>setScopeId</c>;
    /// consumed once scoped styles land with the compiler areas).
    /// </summary>
    public Action<TNode, string>? SetScopeId { get; init; }

    /// <summary>
    /// Optional: clones a node — used to stamp out repeated static content cheaply (upstream:
    /// <c>cloneNode</c>).
    /// </summary>
    public Func<TNode, TNode>? CloneNode { get; init; }

    /// <summary>
    /// Optional: inserts a static markup chunk in one operation (upstream:
    /// <c>insertStaticContent</c>). Required to mount <see cref="VirtualNodeType.Static"/>
    /// vnodes.
    /// </summary>
    public InsertStaticContentDelegate<TNode>? InsertStaticContent { get; init; }
}
