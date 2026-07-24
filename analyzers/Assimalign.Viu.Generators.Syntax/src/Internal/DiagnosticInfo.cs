using System;

using Microsoft.CodeAnalysis;

// The project namespace is nested under Assimalign.Viu.Syntax, so the base cluster's Diagnostic type is
// ambient and shadows Roslyn's; alias the Roslyn type this plumbing produces.
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;

namespace Assimalign.Viu.Generators.Syntax;

/// <summary>
/// A value-equatable diagnostic to emit, carried inside the generator's cached model. Deferring the
/// <see cref="Diagnostic"/> construction to <see cref="ToDiagnostic"/> keeps the pipeline output
/// comparable so unrelated edits stay cached. The <see cref="Descriptor"/> is a stable VIU-prefixed
/// <see cref="DiagnosticDescriptor"/> mapped from a base <c>Assimalign.Viu.Syntax</c> diagnostic, and
/// <see cref="Message"/> is the parser's original human-readable text.
/// </summary>
internal readonly record struct DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo Location, string Message)
    : IEquatable<DiagnosticInfo>
{
    /// <summary>Materializes the Roslyn diagnostic for reporting.</summary>
    /// <returns>The diagnostic, located on the originating <c>.viu</c> file.</returns>
    public RoslynDiagnostic ToDiagnostic()
        => RoslynDiagnostic.Create(Descriptor, Location.ToLocation(), Message);
}
