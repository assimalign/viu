namespace Assimalign.Viu.Components;

/// <summary>
/// Identifies how a host applies one binding to an element.
/// </summary>
public enum ElementBindingKind
{
    /// <summary>A qualified markup attribute.</summary>
    Attribute,

    /// <summary>A host object property.</summary>
    Property,

    /// <summary>A host event subscription.</summary>
    Event
}
