namespace Assimalign.Viu.Testing;

/// <summary>
/// The kinds of node operations the test adapter records — parity with <c>NodeOpTypes</c> in
/// <c>@vue/runtime-test</c> (<c>packages/runtime-test/src/nodeOps.ts</c>), split per create
/// kind. Structural ops are <see cref="Insert"/> and <see cref="Remove"/>.
/// </summary>
public enum TestNodeOperationType
{
    /// <summary>An element was created.</summary>
    CreateElement,

    /// <summary>A text node was created.</summary>
    CreateText,

    /// <summary>A comment node was created.</summary>
    CreateComment,

    /// <summary>A text node's content was set.</summary>
    SetText,

    /// <summary>A node was inserted into a parent (optionally before an anchor).</summary>
    Insert,

    /// <summary>A node was removed from its parent.</summary>
    Remove,

    /// <summary>A single attribute, property, or event binding was patched on an element.</summary>
    PatchAttribute,

    /// <summary>A compiler-produced scoped-style identifier was stamped on an element.</summary>
    SetScopeIdentifier,

    /// <summary>A raw static-markup chunk was inserted in one operation.</summary>
    InsertStaticContent,
}
