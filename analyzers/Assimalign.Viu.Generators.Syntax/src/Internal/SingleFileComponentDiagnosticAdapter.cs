using System;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Assimalign.Viu.Compiler.SingleFileComponent;

using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// The generator-side materialization of the shared projection's host-neutral diagnostics
/// ([V01.01.06.11]): each <see cref="SingleFileComponentDiagnostics"/> catalog entry has exactly one
/// Roslyn <see cref="DiagnosticDescriptor"/> here, with identical id, title, category, help link, and
/// default severity. The Roslyn descriptors deliberately stay in this project — not the projection
/// library — so <c>AnalyzerReleases.Shipped.md</c>/RS2008 release tracking keeps enforcing ID stability
/// in the project that owns <c>EnforceExtendedAnalyzerRules</c>. The 1:1 catalog coverage is pinned by
/// a test, so a new neutral catalog entry without an adapter mapping fails the build's tests rather
/// than throwing at generation time.
/// </summary>
internal static class SingleFileComponentDiagnosticAdapter
{
    private const string Category = "Assimalign.Viu.Generators.Syntax";

    // The stable per-id help-link target: the VIU diagnostic catalog documents every descriptor's ID,
    // origin, severity, and configuration ([V01.01.05.08]). Each descriptor links its own heading anchor.
    private const string HelpLinkBase =
        "https://github.com/assimalign/viu/blob/main/analyzers/Assimalign.Viu.Generators.Syntax/docs/DIAGNOSTICS.md";

    private static string HelpLink(string id) => HelpLinkBase + "#" + id.ToLowerInvariant();

    /// <summary>A recoverable error reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentError = new(
        id: "VIU1001",
        title: "Single-file component parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1001"));

    /// <summary>A warning reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentWarning = new(
        id: "VIU1002",
        title: "Single-file component parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1002"));

    /// <summary>An informational message reported by the <c>.viu</c> block-container parser.</summary>
    internal static readonly DiagnosticDescriptor SingleFileComponentInformation = new(
        id: "VIU1003",
        title: "Single-file component parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1003"));

    /// <summary>A compatibility <c>.vue</c> file is shadowed by its canonical same-base <c>.viu</c> source.</summary>
    internal static readonly DiagnosticDescriptor ConflictingComponentFormats = new(
        id: "VIU1004",
        title: "Conflicting single-file component formats",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1004"));

    /// <summary>A recoverable error reported by the dispatched template parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateError = new(
        id: "VIU1101",
        title: "Single-file component template parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1101"));

    /// <summary>A warning reported by the dispatched template parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateWarning = new(
        id: "VIU1102",
        title: "Single-file component template parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1102"));

    /// <summary>An informational message reported by the dispatched template parse.</summary>
    internal static readonly DiagnosticDescriptor TemplateInformation = new(
        id: "VIU1103",
        title: "Single-file component template parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1103"));

    /// <summary>A recoverable error reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly DiagnosticDescriptor ScriptError = new(
        id: "VIU1201",
        title: "Single-file component script parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1201"));

    /// <summary>A warning reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly DiagnosticDescriptor ScriptWarning = new(
        id: "VIU1202",
        title: "Single-file component script parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1202"));

    /// <summary>An informational message reported by the Roslyn parse of the <c>@script</c> block's C#.</summary>
    internal static readonly DiagnosticDescriptor ScriptInformation = new(
        id: "VIU1203",
        title: "Single-file component script parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1203"));

    /// <summary>A script member conflicts with a member reserved by the generated component scaffold.</summary>
    internal static readonly DiagnosticDescriptor ReservedScriptMember = new(
        id: "VIU1204",
        title: "Single-file component script member is reserved",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1204"));

    /// <summary>An asynchronous script callback returns void and therefore cannot be observed.</summary>
    internal static readonly DiagnosticDescriptor AsynchronousVoidCallback = new(
        id: "VIU1205",
        title: "Asynchronous void callback cannot be observed",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1205"));

    /// <summary>A tag-based script block does not explicitly select Viu's C# script language.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedScriptLanguage = new(
        id: "VIU1206",
        title: "Unsupported single-file component script language",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1206"));

    /// <summary>
    /// A component declares the same kind of surface both by attribute and with its own
    /// <c>Parameters</c>/<c>Events</c> member ([CMP-31]).
    /// </summary>
    internal static readonly DiagnosticDescriptor ConflictingComponentDeclaration = new(
        id: "VIU1207",
        title: "Conflicting component parameter or event declaration",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1207"));

    /// <summary>Two attributed members declare the same component parameter or event name.</summary>
    internal static readonly DiagnosticDescriptor DuplicateComponentDeclaration = new(
        id: "VIU1208",
        title: "Duplicate component parameter or event declaration",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1208"));

    /// <summary>
    /// A <c>[Parameter]</c> or <c>[Event]</c> declaration is on a member shape the generated scaffold
    /// cannot implement, or carries a non-constant argument.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedComponentDeclaration = new(
        id: "VIU1209",
        title: "Unsupported component parameter or event declaration",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1209"));

    /// <summary>A recoverable error reported by the dispatched style CSS parse ([V01.01.06.04]).</summary>
    internal static readonly DiagnosticDescriptor StyleError = new(
        id: "VIU1301",
        title: "Single-file component style parse error",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1301"));

    /// <summary>A warning reported by the dispatched style CSS parse ([V01.01.06.04]).</summary>
    internal static readonly DiagnosticDescriptor StyleWarning = new(
        id: "VIU1302",
        title: "Single-file component style parse warning",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1302"));

    /// <summary>An informational message reported by the dispatched style CSS parse ([V01.01.06.04]).</summary>
    internal static readonly DiagnosticDescriptor StyleInformation = new(
        id: "VIU1303",
        title: "Single-file component style parse information",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1303"));

    /// <summary>
    /// A component usage supplies an attribute the component declares no parameter for. Warning, not
    /// error: an undeclared attribute is a legal fallthrough [CMP-17] ([SFC-USE-2]).
    /// </summary>
    internal static readonly DiagnosticDescriptor UnknownComponentParameter = new(
        id: "VIU1401",
        title: "Component declares no such parameter",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1401"));

    /// <summary>A component usage omits a parameter the component declares required ([SFC-USE-3]).</summary>
    internal static readonly DiagnosticDescriptor MissingRequiredComponentParameter = new(
        id: "VIU1402",
        title: "Required component parameter is not supplied",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1402"));

    /// <summary>
    /// A component usage supplies a value whose type cannot be the declared parameter's type
    /// ([SFC-USE-4]).
    /// </summary>
    internal static readonly DiagnosticDescriptor IncompatibleComponentArgument = new(
        id: "VIU1403",
        title: "Component argument type is incompatible",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink("VIU1403"));

    /// <summary>Materializes the Roslyn diagnostic for reporting.</summary>
    /// <param name="info">The projection's value-equatable neutral diagnostic.</param>
    /// <returns>The diagnostic, located on the originating <c>.viu</c> file.</returns>
    public static RoslynDiagnostic ToDiagnostic(in DiagnosticInfo info)
        => RoslynDiagnostic.Create(ToDescriptor(info.Descriptor), ToLocation(info.Location), info.Message);

    /// <summary>Resolves the Roslyn descriptor materializing a neutral catalog entry.</summary>
    /// <param name="descriptor">The projection library's neutral catalog entry.</param>
    /// <returns>The Roslyn descriptor with the identical id, title, help link, and severity.</returns>
    /// <exception cref="InvalidOperationException">The catalog entry has no adapter mapping (a coverage-test failure).</exception>
    public static DiagnosticDescriptor ToDescriptor(SingleFileComponentDiagnosticDescriptor descriptor)
        => descriptor.Id switch
        {
            "VIU1001" => SingleFileComponentError,
            "VIU1002" => SingleFileComponentWarning,
            "VIU1003" => SingleFileComponentInformation,
            "VIU1004" => ConflictingComponentFormats,
            "VIU1101" => TemplateError,
            "VIU1102" => TemplateWarning,
            "VIU1103" => TemplateInformation,
            "VIU1201" => ScriptError,
            "VIU1202" => ScriptWarning,
            "VIU1203" => ScriptInformation,
            "VIU1204" => ReservedScriptMember,
            "VIU1205" => AsynchronousVoidCallback,
            "VIU1206" => UnsupportedScriptLanguage,
            "VIU1207" => ConflictingComponentDeclaration,
            "VIU1208" => DuplicateComponentDeclaration,
            "VIU1209" => UnsupportedComponentDeclaration,
            "VIU1301" => StyleError,
            "VIU1302" => StyleWarning,
            "VIU1303" => StyleInformation,
            "VIU1401" => UnknownComponentParameter,
            "VIU1402" => MissingRequiredComponentParameter,
            "VIU1403" => IncompatibleComponentArgument,
            _ => throw new InvalidOperationException(
                $"The neutral diagnostic catalog entry '{descriptor.Id}' has no Roslyn descriptor mapping."),
        };

    // Rebuilds a Roslyn Location on the .viu file for reporting — the materialization half of the
    // library's neutral LocationInfo snapshot (which stays free of Roslyn's non-equatable Location).
    private static Location ToLocation(in LocationInfo location)
        => Location.Create(
            location.FilePath,
            TextSpan.FromBounds(location.StartOffset, location.EndOffset),
            new LinePositionSpan(
                new LinePosition(location.StartLine, location.StartCharacter),
                new LinePosition(location.EndLine, location.EndCharacter)));
}
