using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Identifies an assembly's public generated catalog to referencing compilations. The catalog type
/// exposes a static Register(ComponentFactory) method; runtime code never reads this attribute.
/// Specified by <c>[CMP-36]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ComponentCatalogAttribute : Attribute
{
    /// <summary>Records the catalog's compile-time type identity.</summary>
    /// <param name="catalogType">The generated catalog type.</param>
    public ComponentCatalogAttribute(Type catalogType) => CatalogType = catalogType;

    /// <summary>Gets the catalog type for compile-time discovery.</summary>
    public Type CatalogType { get; }
}
