using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing.Benchmarks;

namespace Assimalign.Viu.Testing.Benchmarks.Tests;

// Pins the interop-count harness: scenario discovery, deterministic counters, and the keyed-diff /
// patch-flag guarantees the epic wants locked in by tests ("N node ops == N interop calls", so a scenario
// that should cost one crossing must measure exactly one).
public class InteropCountHarnessTests
{
    [Fact]
    public void Discover_ReturnsTheCanonicalScenarioSet_InAStableOrder()
    {
        var names = InteropScenarioLibrary.Discover().Select(scenario => scenario.Name).ToArray();

        names.ShouldBe(
        [
            "create-1000",
            "create-10000",
            "update-every-10th",
            "swap-rows",
            "select-row",
            "remove-row",
            "append-1000",
            "replace-1000",
            "clear-1000",
        ]);
    }

    [Fact]
    public void MeasureAll_IsDeterministic_AcrossRuns()
    {
        var first = ToTotals(InteropCountHarness.MeasureAll(ScenarioVariant.Optimized));
        var second = ToTotals(InteropCountHarness.MeasureAll(ScenarioVariant.Optimized));

        second.ShouldBe(first);
    }

    [Theory]
    [InlineData("update-every-10th", 100)] // 100 of 1,000 labels change -> 100 targeted set-text crossings.
    [InlineData("swap-rows", 2)]           // Two keyed moves, no content re-patch.
    [InlineData("select-row", 1)]          // One class patch.
    [InlineData("remove-row", 1)]          // The keyed diff removes exactly the one node.
    [InlineData("clear-1000", 1000)]       // 1,000 removals.
    public void OptimizedVariant_MeasuresTheKeyedDiffGuarantee(string scenarioName, int expectedCrossings)
    {
        var totals = ToTotals(InteropCountHarness.MeasureAll(ScenarioVariant.Optimized));

        totals[scenarioName].ShouldBe(expectedCrossings);
    }

    [Fact]
    public void MountingScenarios_AreCreationHeavy_WithStructuralOperations()
    {
        var results = InteropCountHarness.MeasureAll(ScenarioVariant.Optimized)
            .ToDictionary(result => result.Name);

        results["create-1000"].TotalOperationCount.ShouldBeGreaterThan(0);
        results["create-1000"].StructuralOperationCount.ShouldBeGreaterThan(0);
        results["create-1000"].CreateElementCount.ShouldBeGreaterThan(0);
        // Ten times the rows costs about ten times the crossings.
        results["create-10000"].TotalOperationCount
            .ShouldBeGreaterThan(results["create-1000"].TotalOperationCount * 9);
    }

    [Fact]
    public void KeylessBypass_MultipliesCrossings_OnReorderAndRemoval()
    {
        var optimized = ToTotals(InteropCountHarness.MeasureAll(ScenarioVariant.Optimized));
        var keyless = ToTotals(InteropCountHarness.MeasureAll(ScenarioVariant.KeylessBypass));

        // Removing one row from the front is where losing keys hurts most: keyed removes one node, keyless
        // re-patches every shifted row's content. This is the DOM-free analogue of a command-buffer bypass:
        // time-neutral, but a large multiple of the boundary crossings.
        optimized["remove-row"].ShouldBe(1);
        keyless["remove-row"].ShouldBeGreaterThan(1000);

        // A reorder degrades too (positional content patches instead of moves).
        keyless["swap-rows"].ShouldBeGreaterThan(optimized["swap-rows"]);
    }

    private static Dictionary<string, int> ToTotals(IReadOnlyList<InteropCountResult> results)
        => results.ToDictionary(result => result.Name, result => result.TotalOperationCount);
}
