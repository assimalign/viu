using System.Linq;

using Assimalign.Viu.Syntax;

using Shouldly;
using Xunit;

using SyntaxDiagnostic = Assimalign.Viu.Syntax.Diagnostic;
using SyntaxDiagnosticSeverity = Assimalign.Viu.Syntax.DiagnosticSeverity;

namespace Assimalign.Viu.Tooling.SingleFileComponent.Tests;

/// <summary>
/// Pins the host-neutral VIU diagnostic catalog ([V01.01.06.11]): the stable ids, titles, severities,
/// and per-id help links both build-time hosts share, the <see cref="SingleFileComponentDiagnostics.Catalog"/>
/// completeness the generator's adapter-coverage test enumerates, and the value-equality semantics the
/// incremental pipeline's cache keys rely on.
/// </summary>
public sealed class SingleFileComponentDiagnosticCatalogTests
{
    [Fact]
    public void Catalog_CarriesEveryStableIdentity_InIdOrder()
    {
        var expected = new (string Id, SingleFileComponentDiagnosticSeverity Severity)[]
        {
            ("VIU1001", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1002", SingleFileComponentDiagnosticSeverity.Warning),
            ("VIU1003", SingleFileComponentDiagnosticSeverity.Information),
            ("VIU1004", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1101", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1102", SingleFileComponentDiagnosticSeverity.Warning),
            ("VIU1103", SingleFileComponentDiagnosticSeverity.Information),
            ("VIU1201", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1202", SingleFileComponentDiagnosticSeverity.Warning),
            ("VIU1203", SingleFileComponentDiagnosticSeverity.Information),
            ("VIU1204", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1205", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1206", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1207", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1208", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1209", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1301", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1302", SingleFileComponentDiagnosticSeverity.Warning),
            ("VIU1303", SingleFileComponentDiagnosticSeverity.Information),
            ("VIU1401", SingleFileComponentDiagnosticSeverity.Warning),
            ("VIU1402", SingleFileComponentDiagnosticSeverity.Error),
            ("VIU1403", SingleFileComponentDiagnosticSeverity.Error),
        };

        SingleFileComponentDiagnostics.Catalog.Count.ShouldBe(expected.Length);
        foreach (var (entry, (id, severity)) in SingleFileComponentDiagnostics.Catalog.Zip(expected, (entry, pair) => (entry, pair)))
        {
            entry.Id.ShouldBe(id);
            entry.DefaultSeverity.ShouldBe(severity);
            entry.Title.ShouldNotBeNullOrEmpty();
            // A stable, per-id help link into the DIAGNOSTICS.md catalog.
            entry.HelpLink.ShouldEndWith("#" + id.ToLowerInvariant());
        }
    }

    [Theory]
    [InlineData(SyntaxDiagnosticSeverity.Error, "VIU1301")]
    [InlineData(SyntaxDiagnosticSeverity.Warning, "VIU1302")]
    [InlineData(SyntaxDiagnosticSeverity.Information, "VIU1303")]
    [InlineData(SyntaxDiagnosticSeverity.Hidden, "VIU1303")] // Hidden collapses into Information
    public void CreateStyle_MapsEachSeverity_ToItsStyleDescriptor(
        SyntaxDiagnosticSeverity severity,
        string expectedId)
    {
        var diagnostic = new FakeDiagnostic
        {
            Message = "boom",
            Location = new SourceLocation(new Position(0, 1, 1), new Position(3, 1, 4), "abc"),
            Severity = severity,
        };

        var info = SingleFileComponentDiagnostics.CreateStyle(
            "C:/proj/X.viu", diagnostic, new Position(10, 2, 1));

        info.Descriptor.Id.ShouldBe(expectedId);
        // The Viu-defined CssErrorCode rides on the message, like every per-language catalog code.
        info.Message.ShouldContain("42");
    }

    [Fact]
    public void DiagnosticInfo_EqualInputs_AreValueEqual()
    {
        // The neutral DiagnosticInfo is a pipeline cache key: equal inputs must produce equal infos, and
        // the catalog singletons preserve reference identity, so the record equality is cheap and exact —
        // the same semantics the pre-extraction Roslyn-descriptor form had.
        var first = SingleFileComponentDiagnostics.CreateFileRule(
            SingleFileComponentDiagnostics.ConflictingComponentFormats, "shadowed", "C:/proj/Choice.vue");
        var second = SingleFileComponentDiagnostics.CreateFileRule(
            SingleFileComponentDiagnostics.ConflictingComponentFormats, "shadowed", "C:/proj/Choice.vue");

        first.ShouldBe(second);
        ReferenceEquals(first.Descriptor, second.Descriptor).ShouldBeTrue();
    }

    // A minimal concrete base diagnostic so severity branches no parser produces today can be exercised.
    private sealed record FakeDiagnostic : SyntaxDiagnostic
    {
        public override int RawCode => 42;
    }
}
