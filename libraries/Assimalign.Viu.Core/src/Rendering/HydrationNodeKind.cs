namespace Assimalign.Viu;

/// <summary>
/// The kind of an existing platform node the hydration walker is adopting, as reported by a
/// <see cref="HydrationNodeReader{TNode}"/> — the C# stand-in for the <c>Node.nodeType</c> reads
/// in <c>@vue/runtime-core</c>'s hydration module (<c>packages/runtime-core/src/hydration.ts</c>,
/// the <c>DOMNodeTypes</c> checks; https://vuejs.org/guide/scaling-up/ssr.html#client-hydration).
/// The walker branches on this to decide whether a server node matches the vnode it is hydrating
/// against, so the enum is the minimal discriminator hydration needs — element, character data
/// (text), comment, or anything else (a mismatch).
/// </summary>
public enum HydrationNodeKind
{
    /// <summary>An element node (upstream: <c>DOMNodeTypes.ELEMENT</c> — <c>nodeType === 1</c>).</summary>
    Element,

    /// <summary>A text node (upstream: <c>DOMNodeTypes.TEXT</c> — <c>nodeType === 3</c>).</summary>
    Text,

    /// <summary>A comment node (upstream: <c>DOMNodeTypes.COMMENT</c> — <c>nodeType === 8</c>).</summary>
    Comment,

    /// <summary>
    /// Any other node kind. The walker treats it as a mismatch against every vnode type, since the
    /// SSR output only ever emits elements, text, and comments (the marker vocabulary).
    /// </summary>
    Other,
}
