namespace Assimalign.Viu.Components;

/// <summary>
/// Identifies how a host applies one binding to an element.
/// </summary>
/// <remarks>Used by the immutable element vocabulary specified by <c>[CMP-3]</c>.</remarks>
public enum ElementBindingKind
{
    /// <summary>A qualified markup attribute.</summary>
    Attribute,

    /// <summary>A host object property.</summary>
    Property,

    /// <summary>A host event subscription.</summary>
    Event
}
