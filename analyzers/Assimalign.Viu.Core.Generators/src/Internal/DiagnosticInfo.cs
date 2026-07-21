using System;

using Microsoft.CodeAnalysis;

namespace Assimalign.Viu.Core.Generators;

/// <summary>
/// A value-equatable diagnostic to emit, carried inside the generator's cached model. Deferring the
/// <see cref="Diagnostic"/> construction to <see cref="ToDiagnostic"/> keeps the pipeline output
/// comparable so unrelated edits stay cached.
/// </summary>
internal readonly record struct DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo? Location, string MessageArgument)
    : IEquatable<DiagnosticInfo>
{
    /// <summary>Materializes the diagnostic for reporting.</summary>
    /// <returns>The diagnostic.</returns>
    public Diagnostic ToDiagnostic()
        => Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, MessageArgument);
}
