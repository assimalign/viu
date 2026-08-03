using Microsoft.CodeAnalysis;

using Assimalign.Viu.Tooling.SingleFileComponent;

using Shouldly;
using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins the [V01.01.06.11] neutral-catalog ↔ Roslyn-descriptor seam: every entry of the shared
/// projection library's <see cref="SingleFileComponentDiagnostics.Catalog"/> must have exactly one
/// generator-side Roslyn descriptor with the identical id, title, help link, and severity. A new id
/// added to the library without an adapter mapping fails here — a test failure, not a throw at
/// generation time. The Roslyn descriptors deliberately live in the generator project so RS2008
/// release tracking keeps enforcing ID stability where <c>AnalyzerReleases.Shipped.md</c> lives.
/// </summary>
public sealed class SingleFileComponentDiagnosticAdapterTests
{
    [Fact]
    public void ToDescriptor_CoversEveryNeutralCatalogEntry_WithIdenticalIdentity()
    {
        // The shipped catalog is exactly the 16 descriptors recorded in AnalyzerReleases.Shipped.md.
        SingleFileComponentDiagnostics.Catalog.Count.ShouldBe(16);

        foreach (var descriptor in SingleFileComponentDiagnostics.Catalog)
        {
            var materialized = SingleFileComponentDiagnosticAdapter.ToDescriptor(descriptor);

            materialized.Id.ShouldBe(descriptor.Id);
            materialized.Title.ToString().ShouldBe(descriptor.Title);
            materialized.HelpLinkUri.ShouldBe(descriptor.HelpLink);
            materialized.DefaultSeverity.ShouldBe(ExpectedSeverity(descriptor.DefaultSeverity));
            materialized.IsEnabledByDefault.ShouldBeTrue();
        }
    }

    [Fact]
    public void ToDiagnostic_MaterializesTheNeutralLocation_OnTheViuFile()
    {
        // The adapter owns the LocationInfo -> Roslyn Location rebuild the library dropped when its
        // DiagnosticInfo went neutral; pin that the coordinates survive the round trip.
        var info = SingleFileComponentDiagnostics.CreateFileRule(
            SingleFileComponentDiagnostics.ConflictingComponentFormats,
            "shadowed",
            "C:/proj/Choice.vue");

        var diagnostic = SingleFileComponentDiagnosticAdapter.ToDiagnostic(info);

        diagnostic.Id.ShouldBe("VIU1004");
        diagnostic.GetMessage().ShouldBe("shadowed");
        diagnostic.Location.GetLineSpan().Path.ShouldBe("C:/proj/Choice.vue");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(0);
    }

    private static RoslynDiagnosticSeverity ExpectedSeverity(SingleFileComponentDiagnosticSeverity severity)
        => severity switch
        {
            SingleFileComponentDiagnosticSeverity.Error => RoslynDiagnosticSeverity.Error,
            SingleFileComponentDiagnosticSeverity.Warning => RoslynDiagnosticSeverity.Warning,
            _ => RoslynDiagnosticSeverity.Info,
        };
}
