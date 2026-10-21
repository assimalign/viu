using System;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Marks an assembly's public generated server-render catalog for compile-time discovery. Its static
/// Register(ServerRenderRegistry) method is called directly; runtime scanning is forbidden.
/// Specified by <c>[CMP-36]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ServerRenderCatalogAttribute : Attribute
{
    /// <summary>Records the generated catalog type.</summary>
    /// <param name="catalogType">The compile-time catalog identity.</param>
    public ServerRenderCatalogAttribute(Type catalogType) => CatalogType = catalogType;

    /// <summary>Gets the catalog type consumed by referencing generators.</summary>
    public Type CatalogType { get; }
}
