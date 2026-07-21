using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// BenchmarkDotNet wall-clock timings for the renderer's diff/patch over the js-framework-benchmark
/// scenarios, driven through the in-memory test adapter (no browser, no interop). Each case runs the
/// whole scenario end to end over a fresh renderer — the same click-to-settled work js-framework-benchmark
/// times — so a case is self-contained and stable across BenchmarkDotNet's repeated invocations. Timings
/// are environment-relative and reported as artifacts, never gated; the mutation's <em>crossing count</em>
/// (the gated metric) is isolated separately by the interop-count harness, which resets the op log
/// between mount and mutation. The Optimized variant (keyed rows, patch-flagged labels) is measured — the
/// shipped path the numbers should track over time.
/// </summary>
[MemoryDiagnoser]
public class RendererScenarioBenchmarks
{
    /// <summary>The scenario under measurement (BenchmarkDotNet fills this from <see cref="ScenarioSource"/>).</summary>
    [ParamsSource(nameof(ScenarioSource))]
    public InteropScenario Scenario { get; set; } = null!;

    /// <summary>The scenario catalogue BenchmarkDotNet parametrizes over.</summary>
    /// <returns>The discovered scenarios.</returns>
    public IEnumerable<InteropScenario> ScenarioSource() => InteropScenarioLibrary.Discover();

    /// <summary>Runs the selected scenario end to end over a fresh renderer.</summary>
    /// <returns>The scenario's total node operations (consumed so the render is not eliminated).</returns>
    [Benchmark]
    public int RenderScenario() => Scenario.Run(ScenarioVariant.Optimized);
}
