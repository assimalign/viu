using System;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu.Compiler.SingleFileComponent;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Materializes the shared single-file-component projection's host-neutral diagnostics at authored
/// document coordinates, including compilation-wide component-usage validation ([V01.01.12.07.15],
/// #336). The language server and Visual Studio remain transport adapters over this result.
/// </summary>
internal static class LanguageDiagnosticProvider
{
    /// <summary>Gets template, style, and component-usage diagnostics for one live projection.</summary>
    /// <param name="projection">The single projection shared by the publication round.</param>
    /// <param name="componentCatalog">
    /// The build-equivalent component declaration catalog, or <see langword="null"/> when the host
    /// has not resolved a compilation closure.
    /// </param>
    /// <param name="cancellationToken">The token cancelling diagnostic materialization.</param>
    /// <returns>The authored-position language diagnostics.</returns>
    internal static IReadOnlyList<LanguageDiagnostic> GetProjectionDiagnostics(
        SingleFileComponentProjectionResult projection,
        ComponentDeclarationCatalog? componentCatalog,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<DiagnosticInfo>(
            projection.Diagnostics.Count + projection.ComponentUsages.Count);
        foreach (var diagnostic in projection.Diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The incremental syntax workspace remains authoritative for container recovery and
            // tag-based compatibility. Re-running the full projection over an incomplete edit can
            // produce secondary VIU10xx, VIU1201-VIU1203, or VIU1206 errors that replace or duplicate
            // those stable diagnostics. The build's VIU1204, VIU1205, and VIU1207-VIU1209 rules are
            // primary authored-member diagnostics and are additive alongside the nested template/style
            // bands; VIU14xx is appended below after a compilation-wide catalog resolves.
            if (diagnostic.Descriptor.Id.StartsWith("VIU11", StringComparison.Ordinal) ||
                diagnostic.Descriptor.Id.StartsWith("VIU13", StringComparison.Ordinal) ||
                diagnostic.Descriptor.Id is
                    "VIU1204" or
                    "VIU1205" or
                    "VIU1207" or
                    "VIU1208" or
                    "VIU1209")
            {
                diagnostics.Add(diagnostic);
            }
        }

        // A loose file or semantic-engine miss has no compilation-wide declaration closure.
        // Treating the empty fallback catalog as authoritative would falsely report every component
        // as unresolved; VIU14xx activates only after the engine resolves the catalog. A resolved,
        // genuinely empty catalog remains a value here and correctly reports unresolved usages.
        if (componentCatalog is { } resolvedCatalog)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ComponentUsageValidator.Validate(
                projection.ComponentUsages,
                resolvedCatalog,
                diagnostics);
        }

        var result = new LanguageDiagnostic[diagnostics.Count];
        for (var index = 0; index < diagnostics.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[index] = ToLanguageDiagnostic(diagnostics[index]);
        }

        return result;
    }

    private static LanguageDiagnostic ToLanguageDiagnostic(DiagnosticInfo diagnostic)
        => new(
            new LanguageRange(
                new LanguagePosition(
                    diagnostic.Location.StartLine,
                    diagnostic.Location.StartCharacter),
                new LanguagePosition(
                    diagnostic.Location.EndLine,
                    diagnostic.Location.EndCharacter)),
            diagnostic.Descriptor.DefaultSeverity switch
            {
                SingleFileComponentDiagnosticSeverity.Information =>
                    LanguageDiagnosticSeverity.Information,
                SingleFileComponentDiagnosticSeverity.Warning =>
                    LanguageDiagnosticSeverity.Warning,
                _ => LanguageDiagnosticSeverity.Error,
            },
            diagnostic.Descriptor.Id,
            diagnostic.Message,
            "viu");
}
