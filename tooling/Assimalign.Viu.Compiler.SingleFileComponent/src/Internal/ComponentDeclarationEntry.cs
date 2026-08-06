namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One statically readable component declaration in the build-time catalog: the component's resolvable
/// name and the parameters it declares by attribute. Only an attribute-declared surface appears here —
/// an imperative <c>Parameters</c> collection is arbitrary C# whose contents no compiler can read, so
/// such a component is absent from the catalog and its usages are never validated ([SFC-USE-5]).
/// Value-equatable, so the catalog participates in the incremental generator's caching.
/// </summary>
/// <param name="Name">The component's resolvable name — its generated class name, or its type name in metadata.</param>
/// <param name="Parameters">The attribute-declared input parameters.</param>
internal readonly record struct ComponentDeclarationEntry(
    string Name,
    EquatableArray<ComponentParameterDeclaration> Parameters);
