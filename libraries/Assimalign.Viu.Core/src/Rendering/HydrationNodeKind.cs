namespace Assimalign.Viu;

/// <summary>
/// Identifies an existing host node while a server-rendered tree is being hydrated.
/// </summary>
/// <remarks>
/// The hydration walker must classify a node it did not create, so this enumeration is the
/// host-neutral form of the node-kind question — a host reports it without Core knowing anything
/// about DOM node types.
/// </remarks>
public enum HydrationNodeKind
{
    /// <summary>An element node.</summary>
    Element,

    /// <summary>A text node.</summary>
    Text,

    /// <summary>A comment node.</summary>
    Comment,

    /// <summary>A host node that cannot be adopted by the component renderer.</summary>
    Other,
}
