namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One statically readable component declaration in the build-time catalog: the component's resolvable
/// name and the parameters it declares by attribute. An imperative <c>Parameters</c> collection still
/// contributes the component identity, but marks its parameter surface unknown because arbitrary C#
/// cannot be inspected by the compiler ([SFC-USE-5]).
/// Value-equatable, so the catalog participates in the incremental generator's caching.
/// </summary>
/// <param name="Name">The component's resolvable name — its generated class name, or its type name in metadata.</param>
/// <param name="Parameters">The attribute-declared input parameters.</param>
public readonly record struct ComponentDeclarationEntry(
    string Name,
    EquatableArray<ComponentParameterDeclaration> Parameters)
{
    /// <summary>
    /// Whether <see cref="Parameters"/> is the component's complete surface. False for an imperative
    /// <c>Parameters</c> member, whose contents remain unknown while its component identity stays
    /// resolvable. The default is true, and keeping this marker outside the primary constructor
    /// preserves the original two-argument constructor and deconstruction member shapes for known
    /// declarative surfaces.
    /// </summary>
    public bool IsParameterSurfaceKnown { get; init; } = true;
}
