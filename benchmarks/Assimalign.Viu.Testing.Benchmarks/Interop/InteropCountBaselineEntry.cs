namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// One scenario's reviewed interop-crossing ceiling in the baseline manifest. Interop counts are
/// deterministic and machine-independent, so <see cref="Tolerance"/> defaults to zero — any increase over
/// <see cref="TotalOperationCount"/> is a regression. A ceiling is raised only by an explicit, reviewed
/// edit to the manifest, exactly like the publish-size budget: no silent ratcheting.
/// </summary>
public sealed class InteropCountBaselineEntry
{
    /// <summary>The scenario id (matches <see cref="InteropScenario.Name"/>).</summary>
    public string Name { get; init; } = "";

    /// <summary>The reviewed total-operation ceiling for the scenario's measured step.</summary>
    public int TotalOperationCount { get; init; }

    /// <summary>The permitted increase over the ceiling before it counts as a regression (default 0).</summary>
    public int Tolerance { get; init; }
}
