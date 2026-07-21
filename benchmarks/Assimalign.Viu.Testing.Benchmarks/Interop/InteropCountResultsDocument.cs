namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The machine-readable results a run persists (per the acceptance criterion "results are persisted per
/// build, machine-readable, with baseline comparison") — the interop-count analogue of the publish-size
/// results JSON. CI uploads it as an artifact so a build's crossings are inspectable after the fact.
/// </summary>
public sealed class InteropCountResultsDocument
{
    /// <summary>When the run was measured (round-trip ISO-8601, UTC).</summary>
    public string GeneratedUtc { get; init; } = "";

    /// <summary>The tree shape the scenarios were measured under.</summary>
    public string Variant { get; init; } = "";

    /// <summary>Whether the run was within the reviewed baseline (null when measured without a baseline).</summary>
    public bool? WithinBaseline { get; init; }

    /// <summary>The per-scenario measured results.</summary>
    public InteropCountResult[] Scenarios { get; init; } = [];
}
