namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// One <c>&lt;SomeComponent …&gt;</c> usage found in a compiled template, reduced to the value-equatable
/// manifest the build-time checker consumes ([SFC-USE-1]). The projection produces it without knowing
/// which components exist; the host joins it against the resolved declaration catalog, which keeps the
/// per-file projection cacheable while the catalog is a compilation-wide input.
/// </summary>
/// <param name="Tag">The component tag exactly as authored.</param>
/// <param name="Location">The already file-composed source range of the tag name.</param>
/// <param name="Properties">The usage's properties, in source order.</param>
/// <param name="SuppliesUnknownArguments">
/// Whether the usage can supply arguments the manifest cannot enumerate — an argument-less
/// <c>v-bind="…"</c> spread, or a dynamic <c>:[name]</c> argument. Validation skips such a usage
/// entirely rather than reporting a parameter the spread may well be supplying [SFC-USE-5].
/// </param>
internal readonly record struct ComponentUsage(
    string Tag,
    LocationInfo Location,
    EquatableArray<ComponentUsageProperty> Properties,
    bool SuppliesUnknownArguments);
