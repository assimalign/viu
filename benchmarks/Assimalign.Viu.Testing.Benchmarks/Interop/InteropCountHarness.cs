using System.Collections.Generic;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Runs the whole scenario catalogue through the in-memory renderer and returns each measured step's
/// interop counts — the "scenario discovery + counters" plumbing the acceptance criteria call for.
/// Deterministic: the same variant always yields the same counts, which is what makes them gate-able.
/// </summary>
public static class InteropCountHarness
{
    /// <summary>Measures every discovered scenario under <paramref name="variant"/>.</summary>
    /// <param name="variant">The tree shape to measure.</param>
    /// <returns>The per-scenario results, in catalogue order.</returns>
    public static IReadOnlyList<InteropCountResult> MeasureAll(ScenarioVariant variant)
    {
        var scenarios = InteropScenarioLibrary.Discover();
        var results = new List<InteropCountResult>(scenarios.Count);
        foreach (var scenario in scenarios)
        {
            results.Add(scenario.Measure(variant));
        }
        return results;
    }
}
