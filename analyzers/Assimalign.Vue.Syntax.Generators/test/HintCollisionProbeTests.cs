using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Xunit;

namespace Assimalign.Vue.Syntax.Generators.Tests;

public sealed class HintCollisionProbeTests
{
    private const string Source = "@template {\n    <div>x</div>\n}\n";

    [Fact]
    public void TwoFilesOutsideProjectDirectory_SameLeafName()
    {
        var fileA = new InMemoryAdditionalText("C:/other/A/Button.viu", Source);
        var fileB = new InMemoryAdditionalText("C:/other/B/Button.viu", Source);
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(fileA, fileB), "Demo", "C:/proj");

        driver = driver.RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        Assert.Null(result.Exception);
        Assert.Equal(2, result.GeneratedSources.Length);
    }

    [Fact]
    public void TwoFilesInProject_NamesSanitizeToSameIdentifier()
    {
        var fileA = new InMemoryAdditionalText("C:/proj/Foo-Bar.viu", Source);
        var fileB = new InMemoryAdditionalText("C:/proj/Foo_Bar.viu", Source);
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(fileA, fileB), "Demo", "C:/proj");

        driver = driver.RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        Assert.Null(result.Exception);
        Assert.Equal(2, result.GeneratedSources.Length);
    }

    [Fact]
    public void TemplateDiagnostic_LineColumn_MapsIntoViuFile()
    {
        // Error inside @template on file line 2 (content line 1), "{{ message" opens at file col 5.
        const string source = "@template {\n    {{ message\n}\n";
        var outcome = GeneratorTestHarness.Run("C:/proj/Counter.viu", source, "Demo", "C:/proj");
        var span = outcome.Diagnostics.Single().Location.GetLineSpan();
        Assert.Equal(1, span.StartLinePosition.Line);      // zero-based -> file line 2
        Assert.Equal(4, span.StartLinePosition.Character); // zero-based -> file col 5
    }
}
