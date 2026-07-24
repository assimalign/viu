using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

// End-to-end pins for hint-name uniqueness through the real generator driver: Roslyn's AddSource
// throws on a duplicate hint name and the exception kills the entire generator run, so colliding
// inputs must emit distinct sources rather than fail ([V01.01.06.02] review follow-up). The
// resolver-level unit pins live in SingleFileComponentGeneratorTests; these run the whole pipeline.
public sealed class HintNameCollisionTests
{
    private const string Source = "@template {\n    <div>x</div>\n}\n";

    [Fact]
    public void TwoFilesOutsideProjectDirectory_WithTheSameLeafName_BothEmit()
    {
        // Linked files outside the project directory cannot use a relative path in the hint, so the
        // path-hash disambiguator must keep them apart.
        var fileA = new InMemoryAdditionalText("C:/other/A/Button.viu", Source);
        var fileB = new InMemoryAdditionalText("C:/other/B/Button.viu", Source);
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(fileA, fileB), "Demo", "C:/proj");

        driver = driver.RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Exception.ShouldBeNull();
        result.GeneratedSources.Length.ShouldBe(2);
    }

    [Fact]
    public void TwoFilesWhoseNamesSanitizeToTheSameIdentifier_BothEmit()
    {
        // Foo-Bar.viu and Foo_Bar.viu both sanitize to the class name Foo_Bar; the lossy
        // sanitization triggers the path-hash disambiguator on the hint name.
        var fileA = new InMemoryAdditionalText("C:/proj/Foo-Bar.viu", Source);
        var fileB = new InMemoryAdditionalText("C:/proj/Foo_Bar.viu", Source);
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(fileA, fileB), "Demo", "C:/proj");

        driver = driver.RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Exception.ShouldBeNull();
        result.GeneratedSources.Length.ShouldBe(2);
    }

    [Fact]
    public void TemplateDiagnosticOnTheBlockStartLine_AddsTheBlockColumn()
    {
        // The first-line branch of the block-to-file composition: the error sits on the block
        // content's FIRST line ("{{ message" opening at file column 5), so the block's start column
        // adds to the relative column — the case the multi-line composition test cannot cover.
        const string source = "@template {\n    {{ message\n}\n";

        var outcome = GeneratorTestHarness.Run("C:/proj/Counter.viu", source, "Demo", "C:/proj");

        var span = outcome.Diagnostics.Single().Location.GetLineSpan();
        span.StartLinePosition.Line.ShouldBe(1);      // zero-based -> file line 2
        span.StartLinePosition.Character.ShouldBe(4); // zero-based -> file column 5
    }
}
