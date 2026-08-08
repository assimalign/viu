using System;
using System.Collections.Generic;
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
    private const string Source = "<template>\n    <div>x</div>\n</template>\n";

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
        ComponentSources(result).Count.ShouldBe(2);
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
        ComponentSources(result).Count.ShouldBe(2);
    }

    [Fact]
    public void TwoFilesWhoseBaseNamesDifferOnlyByCase_BothEmit()
    {
        // [SFC-CG-5] Roslyn compares hint names with OrdinalIgnoreCase, so Choice and choice are ONE
        // name to AddSource even though the two files are two distinct components. Two canonical .viu
        // files are never merged by .vue shadowing [VUE-7], so this collision forms on every operating
        // system - including Windows, where the case-insensitive path identity that hides the .viu/.vue
        // form of the collision does not apply ([V01.01.06.10.01]).
        var fileA = new InMemoryAdditionalText("C:/proj/Components/Choice.viu", Source);
        var fileB = new InMemoryAdditionalText("C:/proj/Components/choice.viu", Source);
        var driver = GeneratorTestHarness.CreateDriver(
            ImmutableArray.Create<AdditionalText>(fileA, fileB), "Demo", "C:/proj");

        driver = driver.RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Exception.ShouldBeNull();
        var componentSources = ComponentSources(result);
        componentSources.Count.ShouldBe(2);
        componentSources
            .Select(source => source.HintName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            .ShouldBe(2);
    }

    [Fact]
    public void CaseCollidingFiles_InEitherEnumerationOrder_TakeTheSameHintNames()
    {
        // [SFC-CG-5] The discriminator is a hash of the file's own exact-cased path, so it is a pure
        // function of the input set: reversing the order MSBuild presents the files in cannot move a
        // single generated-file identity.
        var fileA = new InMemoryAdditionalText("C:/proj/Components/Choice.viu", Source);
        var fileB = new InMemoryAdditionalText("C:/proj/Components/choice.viu", Source);

        var forward = HintNames(ImmutableArray.Create<AdditionalText>(fileA, fileB));
        var reversed = HintNames(ImmutableArray.Create<AdditionalText>(fileB, fileA));

        forward.ShouldBe(reversed);
        forward.Count.ShouldBe(2);
    }

    [Fact]
    public void ComponentsThatCollideWithNothing_KeepTheirReadableHintNames()
    {
        // [SFC-CG-5] The identity-preservation bar: the discriminator appears ONLY inside a colliding
        // group. Counter and Views/Admin/Panel keep the exact hint names the derivation produced before
        // the collision rule existed, while the two Choice components - and only they - take the
        // 8-hex-digit path hash ([V01.01.06.10.01]).
        var files = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("C:/proj/Counter.viu", Source),
            new InMemoryAdditionalText("C:/proj/Views/Admin/Panel.viu", Source),
            new InMemoryAdditionalText("C:/proj/Components/Choice.viu", Source),
            new InMemoryAdditionalText("C:/proj/Components/choice.viu", Source));

        var hintNames = HintNames(files);

        hintNames.ShouldContain("Counter.SingleFileComponent.g.cs");
        hintNames.ShouldContain("Views.Admin.Panel.SingleFileComponent.g.cs");
        hintNames.ShouldNotContain("Components.Choice.SingleFileComponent.g.cs");
        hintNames.ShouldNotContain("Components.choice.SingleFileComponent.g.cs");
        hintNames.Count(name => name.StartsWith("Components.", StringComparison.Ordinal)).ShouldBe(2);
        foreach (var name in hintNames.Where(name => name.StartsWith("Components.", StringComparison.Ordinal)))
        {
            // Components.<Base>.<8 hex digits>.SingleFileComponent.g.cs
            var discriminator = name.Split('.')[2];
            discriminator.Length.ShouldBe(8);
            discriminator.ShouldAllBe(character => Uri.IsHexDigit(character));
        }
    }

    private static IReadOnlyList<string> HintNames(ImmutableArray<AdditionalText> files)
    {
        var driver = GeneratorTestHarness.CreateDriver(files, "Demo", "C:/proj")
            .RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Exception.ShouldBeNull();
        return ComponentSources(result)
            .Select(source => source.HintName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<GeneratedSourceResult> ComponentSources(GeneratorRunResult result)
        => result.GeneratedSources
            .Where(source => source.HintName.EndsWith(
                ".SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void TemplateDiagnosticOnTheBlockStartLine_AddsTheBlockColumn()
    {
        // The first-line branch of the block-to-file composition: tag-block content may begin INLINE
        // right after the opening tag's `>`, so an error on the content's FIRST line must add the
        // block's start column ("{{ message" opens at file column 15 = content start 11 + relative
        // column 5 - 1) — the case the multi-line composition test cannot cover.
        const string source = "<template>    {{ message\n</template>\n";

        var outcome = GeneratorTestHarness.Run("C:/proj/Counter.viu", source, "Demo", "C:/proj");

        var span = outcome.Diagnostics.Single().Location.GetLineSpan();
        span.StartLinePosition.Line.ShouldBe(0);       // zero-based -> file line 1
        span.StartLinePosition.Character.ShouldBe(14); // zero-based -> file column 15
    }
}
