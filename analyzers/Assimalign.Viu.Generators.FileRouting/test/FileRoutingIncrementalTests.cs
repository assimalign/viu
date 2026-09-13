using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Generators.FileRouting.Tests;

public sealed class FileRoutingIncrementalTests
{
    [Fact]
    public void Generate_UnrelatedCSharpEdit_CachesFileReadTableAndEmission()
    {
        var compilation = GeneratorTestHarness.Compilation("class First {}");
        var driver = GeneratorTestHarness.Driver(new[] { GeneratorTestHarness.Page("Index.viu") }).RunGenerators(compilation);
        var before = GeneratorTestHarness.Source(driver.GetRunResult().Results.Single());
        driver = driver.RunGenerators(GeneratorTestHarness.Compilation("class Second {}"));
        var result = driver.GetRunResult().Results.Single();
        GeneratorTestHarness.Source(result).ShouldBe(before);
        AssertCached(result, "FileRoutingFiles");
        AssertCached(result, "FileRoutingTable");
        result.TrackedOutputSteps.SelectMany(pair => pair.Value).SelectMany(step => step.Outputs)
            .ShouldAllBe(output => output.Reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Generate_EquivalentAdditionalTextReplacement_DoesNotRebuildTable()
    {
        var page = GeneratorTestHarness.Page("Page.viu");
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new[] { page }).RunGenerators(compilation);
        driver = driver.ReplaceAdditionalText(page, GeneratorTestHarness.Page("Page.viu")).RunGenerators(compilation);
        var result = driver.GetRunResult().Results.Single();
        result.TrackedSteps["FileRoutingFiles"].SelectMany(step => step.Outputs)
            .ShouldAllBe(output => output.Reason == IncrementalStepRunReason.Unchanged);
        AssertCached(result, "FileRoutingTable");
    }

    [Fact]
    public void Generate_UnrelatedBuildPropertyEdit_DoesNotRebuildTable()
    {
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new[] { GeneratorTestHarness.Page("Page.viu") }).RunGenerators(compilation);
        driver = driver.WithUpdatedAnalyzerConfigOptions(GeneratorTestHarness.Options(new Dictionary<string, string> { ["Configuration"] = "Release" }))
            .RunGenerators(compilation);
        AssertCached(driver.GetRunResult().Results.Single(), "FileRoutingTable");
    }

    [Fact]
    public void Generate_PageTextEdit_RebuildsRouteTable()
    {
        var page = GeneratorTestHarness.Page("Page.viu");
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new[] { page }).RunGenerators(compilation);
        driver = driver.ReplaceAdditionalText(page, GeneratorTestHarness.Page("Page.viu", FileRoutingDiagnosticsTests.Route("path = \"/changed\";")))
            .RunGenerators(compilation);
        var result = driver.GetRunResult().Results.Single();
        result.Diagnostics.ShouldBeEmpty();
        GeneratorTestHarness.Source(result).ShouldContain("\"/changed\"");
        result.TrackedSteps["FileRoutingTable"].SelectMany(step => step.Outputs)
            .ShouldAllBe(output => output.Reason == IncrementalStepRunReason.Modified);
    }

    [Fact]
    public void Generate_PagesDirectoryEdit_ChangesSelectedFiles()
    {
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new[] { GeneratorTestHarness.Page("Page.viu"), new InMemoryAdditionalText("/project/Screens/Other.viu", "") })
            .RunGenerators(compilation);
        driver = driver.WithUpdatedAnalyzerConfigOptions(GeneratorTestHarness.Options(new() { ["ViuFileRoutingPagesDirectory"] = "Screens" }))
            .RunGenerators(compilation);
        var source = GeneratorTestHarness.Source(driver.GetRunResult().Results.Single());
        source.ShouldContain("\"Other\", false");
        source.ShouldNotContain("\"Page\", false");
    }

    [Fact]
    public void Generate_PagePathEdit_RebuildsPathAndIdentity()
    {
        var page = GeneratorTestHarness.Page("First.viu");
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new[] { page }).RunGenerators(compilation);
        driver = driver.ReplaceAdditionalText(page, GeneratorTestHarness.Page("Second.viu")).RunGenerators(compilation);
        var source = GeneratorTestHarness.Source(driver.GetRunResult().Results.Single());
        source.ShouldContain("(\"/second\", \"second\", \"Second\", false)");
        source.ShouldNotContain("First");
    }

    [Fact]
    public void Generate_RootNamespaceEdit_UpdatesGeneratedNamespace()
    {
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new[] { GeneratorTestHarness.Page("Page.viu") }).RunGenerators(compilation);
        driver = driver.WithUpdatedAnalyzerConfigOptions(GeneratorTestHarness.Options(new() { ["RootNamespace"] = "Other.Application" }))
            .RunGenerators(compilation);
        GeneratorTestHarness.Source(driver.GetRunResult().Results.Single()).ShouldContain("namespace Other.Application;");
    }

    [Fact]
    public void Generate_DisablingAndReenablingAfterInitialRun_RemovesAndRestoresGeneratedSource()
    {
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new[] { GeneratorTestHarness.Page("Page.viu") }).RunGenerators(compilation);
        driver = driver.WithUpdatedAnalyzerConfigOptions(GeneratorTestHarness.Options(new() { ["ViuFileRoutingEnabled"] = "false" }))
            .RunGenerators(compilation);
        driver.GetRunResult().Results.Single().GeneratedSources.ShouldBeEmpty();
        driver = driver.WithUpdatedAnalyzerConfigOptions(GeneratorTestHarness.Options(new() { ["ViuFileRoutingEnabled"] = "true" }))
            .RunGenerators(compilation);
        GeneratorTestHarness.Source(driver.GetRunResult().Results.Single()).ShouldContain("\"Page\", false");
    }

    [Fact]
    public void Generate_NonPageTextEdit_DoesNotRebuildTable()
    {
        var unrelated = new InMemoryAdditionalText("/project/Components/Other.viu", "");
        var compilation = GeneratorTestHarness.Compilation();
        var driver = GeneratorTestHarness.Driver(new AdditionalText[] { GeneratorTestHarness.Page("Page.viu"), unrelated }).RunGenerators(compilation);
        driver = driver.ReplaceAdditionalText(unrelated, new InMemoryAdditionalText(unrelated.Path, "<template><span /></template>"))
            .RunGenerators(compilation);
        AssertCached(driver.GetRunResult().Results.Single(), "FileRoutingTable");
    }

    private static void AssertCached(GeneratorRunResult result, string trackingName)
        => result.TrackedSteps[trackingName].SelectMany(step => step.Outputs)
            .ShouldAllBe(output => output.Reason == IncrementalStepRunReason.Cached);
}
