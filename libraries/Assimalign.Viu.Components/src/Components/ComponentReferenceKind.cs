namespace Assimalign.Viu.Components;

/// <summary>
/// Identifies how a component definition is resolved without activating it.
/// </summary>
/// <remarks>Specified by <c>[CMP-7]</c>.</remarks>
public enum ComponentReferenceKind
{
    /// <summary>The definition is resolved by a compile-time component type token.</summary>
    Type,

    /// <summary>The definition is resolved by an application-registered name.</summary>
    RegisteredName
}
