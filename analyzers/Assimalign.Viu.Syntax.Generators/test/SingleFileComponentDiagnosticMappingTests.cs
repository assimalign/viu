using Microsoft.CodeAnalysis;

using Assimalign.Viu.Syntax;

using Shouldly;
using Xunit;

using SyntaxDiagnostic = Assimalign.Viu.Syntax.Diagnostic;
using SyntaxDiagnosticSeverity = Assimalign.Viu.Syntax.DiagnosticSeverity;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Syntax.Generators.Tests;

/// <summary>
/// Pins the generator-surfacing conventions ([V01.01.05.08]): the stable VIU descriptor shape (ID,
/// category, help link, default severity, enabled-by-default) and the base-<see cref="SyntaxDiagnostic"/>
/// severity → Roslyn-descriptor mapping across every severity, including the Warning/Information/Hidden
/// branches no current parser produces yet but the machinery must already route (Vue's error-vs-warning
/// split is pinned on the base diagnostic at parse time, per the addendum on issue #55). Roslyn diagnostics/
/// analyzer conventions: https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/.
/// </summary>
public sealed class SingleFileComponentDiagnosticMappingTests
{
    private const string Category = "Assimalign.Viu.Syntax.Generators";

    [Fact]
    public void EveryDescriptor_CarriesStableIdCategoryHelpLinkAndSeverity()
    {
        var descriptors = new (DiagnosticDescriptor Descriptor, string Id, RoslynDiagnosticSeverity Severity)[]
        {
            (SingleFileComponentDiagnostics.SingleFileComponentError, "VIU1001", RoslynDiagnosticSeverity.Error),
            (SingleFileComponentDiagnostics.SingleFileComponentWarning, "VIU1002", RoslynDiagnosticSeverity.Warning),
            (SingleFileComponentDiagnostics.SingleFileComponentInformation, "VIU1003", RoslynDiagnosticSeverity.Info),
            (SingleFileComponentDiagnostics.TemplateError, "VIU1101", RoslynDiagnosticSeverity.Error),
            (SingleFileComponentDiagnostics.TemplateWarning, "VIU1102", RoslynDiagnosticSeverity.Warning),
            (SingleFileComponentDiagnostics.TemplateInformation, "VIU1103", RoslynDiagnosticSeverity.Info),
            (SingleFileComponentDiagnostics.ScriptError, "VIU1201", RoslynDiagnosticSeverity.Error),
            (SingleFileComponentDiagnostics.ScriptWarning, "VIU1202", RoslynDiagnosticSeverity.Warning),
            (SingleFileComponentDiagnostics.ScriptInformation, "VIU1203", RoslynDiagnosticSeverity.Info),
        };

        foreach (var (descriptor, id, severity) in descriptors)
        {
            descriptor.Id.ShouldBe(id);
            descriptor.Category.ShouldBe(Category);
            descriptor.DefaultSeverity.ShouldBe(severity);
            descriptor.IsEnabledByDefault.ShouldBeTrue();
            // A stable, per-id help link (the issue's "a category, and a help link" acceptance criterion).
            descriptor.HelpLinkUri.ShouldNotBeNullOrEmpty();
            descriptor.HelpLinkUri.ShouldContain(id.ToLowerInvariant());
        }
    }

    [Theory]
    [InlineData(SyntaxDiagnosticSeverity.Error, false, "VIU1001")]
    [InlineData(SyntaxDiagnosticSeverity.Warning, false, "VIU1002")]
    [InlineData(SyntaxDiagnosticSeverity.Information, false, "VIU1003")]
    [InlineData(SyntaxDiagnosticSeverity.Hidden, false, "VIU1003")]     // Hidden collapses into Information
    [InlineData(SyntaxDiagnosticSeverity.Error, true, "VIU1101")]
    [InlineData(SyntaxDiagnosticSeverity.Warning, true, "VIU1102")]
    [InlineData(SyntaxDiagnosticSeverity.Information, true, "VIU1103")]
    [InlineData(SyntaxDiagnosticSeverity.Hidden, true, "VIU1103")]      // Hidden collapses into Information
    public void Create_MapsEachBaseSeverity_ToItsDescriptor(
        SyntaxDiagnosticSeverity severity,
        bool fromTemplate,
        string expectedId)
    {
        var diagnostic = new FakeDiagnostic
        {
            Message = "boom",
            Location = new SourceLocation(new Position(0, 1, 1), new Position(3, 1, 4), "abc"),
            Severity = severity,
        };

        var info = SingleFileComponentDiagnostics.Create("C:/proj/X.viu", diagnostic, fromTemplate, blockContentStart: null);

        info.Descriptor.Id.ShouldBe(expectedId);
        // The per-language RawCode rides on the message so the unbounded code catalog stays visible.
        info.Message.ShouldContain("42");
    }

    // A minimal concrete base diagnostic so the mapping's Warning/Information/Hidden branches — which no
    // parser in the cluster produces today — can be exercised directly.
    private sealed record FakeDiagnostic : SyntaxDiagnostic
    {
        public override int RawCode => 42;
    }
}
