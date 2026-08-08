namespace Assimalign.Viu.Testing;

/// <summary>Identifies an observable operation performed by the in-memory host.</summary>
/// <remarks>Specified by <c>[RND-HOST-1]</c>, <c>[RND-HOST-4]</c>, and <c>[CONF-3]</c>.</remarks>
public enum TestNodeOperationType
{
    /// <summary>An element was created.</summary>
    CreateElement,

    /// <summary>A text node was created.</summary>
    CreateText,

    /// <summary>A comment node was created.</summary>
    CreateComment,

    /// <summary>Character data changed.</summary>
    SetText,

    /// <summary>A node was inserted or moved.</summary>
    Insert,

    /// <summary>A node was removed.</summary>
    Remove,

    /// <summary>An immutable element binding difference was applied.</summary>
    PatchAttribute,

    /// <summary>A compiler-trusted static payload was inserted.</summary>
    InsertStaticContent,

    /// <summary>The buffered-host commit boundary was reached.</summary>
    Commit,
}
