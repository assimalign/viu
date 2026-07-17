using Microsoft.CodeAnalysis;

namespace Assimalign.Vue.Reactivity.Generators;

/// <summary>
/// Stable diagnostic descriptors for <c>[Reactive]</c> misuse. IDs are frozen (they may appear in
/// suppressions and code fixes), use the <c>VUER</c> prefix, and carry code-fix-friendly messages.
/// </summary>
internal static class ReactiveDiagnostics
{
    private const string Category = "Assimalign.Vue.Reactivity.Generators";

    /// <summary>The attributed type is not <c>partial</c>, so the generator cannot add the implementation part.</summary>
    internal static readonly DiagnosticDescriptor NotPartial = new(
        id: "VUER1001",
        title: "Reactive type must be partial",
        messageFormat: "'{0}' is marked reactive but is not declared 'partial'; add the 'partial' modifier so the generator can implement its properties",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The attributed type is <c>static</c>; a reactive object must be instantiable.</summary>
    internal static readonly DiagnosticDescriptor StaticType = new(
        id: "VUER1002",
        title: "Reactive type must not be static",
        messageFormat: "'{0}' is marked reactive but is 'static'; reactive objects must be instantiable",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A reactive partial property lacks a usable getter/setter pair (get-only or init-only).</summary>
    internal static readonly DiagnosticDescriptor UnsupportedProperty = new(
        id: "VUER1003",
        title: "Reactive property must have a getter and a settable setter",
        messageFormat: "Reactive partial property '{0}' must declare both a getter and a non-init setter; it will not be made reactive",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>The type carries both <c>[Reactive]</c> and <c>[ShallowReactive]</c>.</summary>
    internal static readonly DiagnosticDescriptor ConflictingAttributes = new(
        id: "VUER1004",
        title: "Conflicting reactive attributes",
        messageFormat: "'{0}' has both [Reactive] and [ShallowReactive]; apply exactly one",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
