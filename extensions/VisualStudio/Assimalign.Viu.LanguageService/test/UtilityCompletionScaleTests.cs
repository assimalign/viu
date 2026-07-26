using System;
using System.Diagnostics;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Pins the bounds that keep utility completion usable inside Visual Studio. The shared registry
/// expands to well over a hundred thousand themed candidates, so an unbounded or recomputed result
/// is not a slow feature — it is a feature the editor abandons before it can render.
/// </summary>
public class UtilityCompletionScaleTests
{
    private const string DocumentUri = "file:///c:/workspace/Card.viu";

    [Fact]
    public void GetCompletions_UnfilteredClassAttribute_IsBoundedByTheCompletionLimit()
    {
        const string templateLine = "    <div class=\"\"></div>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var caret = templateLine.IndexOf("\"\"", StringComparison.Ordinal) + 1;
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(
            DocumentUri,
            new LanguagePosition(1, caret));

        // Without the bound this position returns the entire themed catalog, which serializes to a
        // payload no language client will accept.
        completions.Count.ShouldBe(LanguageCompletionLimits.MaximumItems);
    }

    [Fact]
    public void GetCompletions_ProjectTheme_ReusesTheExpandedCatalogAcrossRequests()
    {
        const string templateLine = "    <div class=\"gap-\"></div>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var caret = templateLine.IndexOf("gap-", StringComparison.Ordinal) + "gap-".Length;
        var position = new LanguagePosition(1, caret);
        var service = ViuLanguageServices.Create();
        var utilityCssLanguageService =
            service.ShouldBeAssignableTo<IUtilityCssLanguageService>();

        // A configured theme leaves the default-theme fast path, which is the state every real
        // project is in.
        utilityCssLanguageService.ConfigureUtilityStylesheet(
            DocumentUri,
            "@theme { --spacing-card: 2.75rem; }");
        service.OpenDocument(DocumentUri, source, 1);

        service.GetCompletions(DocumentUri, position)
            .ShouldContain(item => item.Label == "gap-4");

        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 20; index++)
        {
            service.GetCompletions(DocumentUri, position);
        }

        stopwatch.Stop();

        // The themed catalog and the project stylesheet compilation are both memoized, so a repeat
        // request costs a prefix filter rather than a full expansion. The budget sits far above a
        // healthy machine and far below the regression it guards: expanding the catalog per request
        // cost roughly three seconds each, which is a full minute for this loop.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetCompletions_DocumentUriCasingDiffersFromOpen_StillResolvesTheOpenDocument()
    {
        const string templateLine = "    <div class=\"gap-\"></div>";
        var source = $"@template {{\n{templateLine}\n}}\n";
        var caret = templateLine.IndexOf("gap-", StringComparison.Ordinal) + "gap-".Length;
        var service = ViuLanguageServices.Create();
        service.OpenDocument("file:///c:/workspace/Card.viu", source, 1);

        // The server host tracks open documents case-insensitively, so the workspace must agree or a
        // client that varies the drive-letter casing silently loses every language feature.
        var completions = service.GetCompletions(
            "file:///C:/workspace/Card.viu",
            new LanguagePosition(1, caret));

        completions.ShouldContain(item => item.Label == "gap-4");
    }
}
