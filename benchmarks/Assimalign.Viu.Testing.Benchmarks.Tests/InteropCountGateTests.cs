using System;
using System.IO;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing.Benchmarks;

namespace Assimalign.Viu.Testing.Benchmarks.Tests;

// Pins the regression gate: the reviewed baseline passes the shipped path, a deliberate keyless bypass
// trips it (the acceptance-criterion demonstration), a scenario without a ceiling is a protection gap,
// and the tolerance is honored.
public class InteropCountGateTests
{
    private static string BaselinePath => Path.Combine(AppContext.BaseDirectory, "baselines", "InteropCounts.json");

    [Fact]
    public void CheckedInBaseline_Loads_WithNotesAndEveryScenario()
    {
        var baseline = InteropCountBaseline.Load(BaselinePath);

        baseline.Note.ShouldNotBeEmpty();
        baseline.Scenarios.Length.ShouldBe(InteropScenarioLibrary.Discover().Count);
    }

    [Fact]
    public void CheckedInBaseline_PassesTheOptimizedShippedPath()
    {
        var baseline = InteropCountBaseline.Load(BaselinePath);

        var comparison = baseline.Compare(InteropCountHarness.MeasureAll(ScenarioVariant.Optimized));

        comparison.Passed.ShouldBeTrue();
        comparison.RegressionCount.ShouldBe(0);
        comparison.MissingBaselineCount.ShouldBe(0);
    }

    [Fact]
    public void DeliberateKeylessBypass_TripsTheGate()
    {
        // The acceptance criterion: a change that keeps timings but multiplies boundary crossings still
        // fails review. Measured against the reviewed (keyed) baseline, the keyless variant regresses.
        var baseline = InteropCountBaseline.Load(BaselinePath);

        var comparison = baseline.Compare(InteropCountHarness.MeasureAll(ScenarioVariant.KeylessBypass));

        comparison.Passed.ShouldBeFalse();
        comparison.RegressionCount.ShouldBeGreaterThan(0);
        var removeRow = comparison.Rows.Single(row => row.Name == "remove-row");
        removeRow.WithinBaseline.ShouldBeFalse();
        removeRow.Delta!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void MeasuredScenarioWithoutACeiling_IsAProtectionGap()
    {
        var baseline = new InteropCountBaseline
        {
            Scenarios = [new InteropCountBaselineEntry { Name = "create-1000", TotalOperationCount = 26005 }],
        };

        var comparison = baseline.Compare(
        [
            new InteropCountResult { Name = "create-1000", TotalOperationCount = 26005 },
            new InteropCountResult { Name = "brand-new-scenario", TotalOperationCount = 5 },
        ]);

        comparison.MissingBaselineCount.ShouldBe(1);
        comparison.Passed.ShouldBeFalse();
    }

    [Fact]
    public void Tolerance_PermitsAnIncreaseUpToTheCeilingPlusTolerance()
    {
        var baseline = new InteropCountBaseline
        {
            Scenarios = [new InteropCountBaselineEntry { Name = "swap-rows", TotalOperationCount = 2, Tolerance = 1 }],
        };

        baseline.Compare([new InteropCountResult { Name = "swap-rows", TotalOperationCount = 3 }])
            .Passed.ShouldBeTrue();
        baseline.Compare([new InteropCountResult { Name = "swap-rows", TotalOperationCount = 4 }])
            .Passed.ShouldBeFalse();
    }

    [Fact]
    public void Load_Throws_WhenTheManifestIsMissing()
    {
        var missing = Path.Combine(AppContext.BaseDirectory, "baselines", "does-not-exist.json");

        Should.Throw<FileNotFoundException>(() => InteropCountBaseline.Load(missing));
    }
}
