using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The reviewed interop-count manifest (<c>benchmarks/baselines/InteropCounts.json</c>) and the gate
/// comparison against it — the interop-crossing analogue of the publish-size budget manifest
/// (<c>scripts/budgets/PublishBudgets.json</c>). Loaded via System.Text.Json <b>source generation</b>
/// (no reflection), so the harness stays trimming/AOT-safe even though the benchmark app itself is never
/// trimmed.
/// </summary>
public sealed class InteropCountBaseline
{
    /// <summary>Free-form provenance notes carried in the manifest (not used by the gate).</summary>
    public string[] Note { get; init; } = [];

    /// <summary>The reviewed per-scenario ceilings.</summary>
    public InteropCountBaselineEntry[] Scenarios { get; init; } = [];

    /// <summary>Loads a baseline manifest from <paramref name="path"/>.</summary>
    /// <param name="path">The manifest path.</param>
    /// <returns>The parsed baseline.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">The manifest does not exist.</exception>
    /// <exception cref="InvalidDataException">The manifest is present but does not parse.</exception>
    public static InteropCountBaseline Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Interop-count baseline not found: {path}", path);
        }
        var json = File.ReadAllText(path);
        var baseline = JsonSerializer.Deserialize(json, InteropCountJsonContext.Default.InteropCountBaseline);
        return baseline ?? throw new InvalidDataException($"Interop-count baseline parsed to null: {path}");
    }

    /// <summary>Compares <paramref name="measured"/> results to this baseline's ceilings.</summary>
    /// <param name="measured">The run's measured results.</param>
    /// <returns>The gate comparison.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measured"/> is null.</exception>
    public InteropCountComparison Compare(IReadOnlyList<InteropCountResult> measured)
    {
        ArgumentNullException.ThrowIfNull(measured);
        var ceilings = new Dictionary<string, InteropCountBaselineEntry>(StringComparer.Ordinal);
        foreach (var entry in Scenarios)
        {
            ceilings[entry.Name] = entry;
        }

        var rows = new List<InteropCountComparisonRow>(measured.Count);
        foreach (var result in measured)
        {
            if (ceilings.TryGetValue(result.Name, out var entry))
            {
                var within = result.TotalOperationCount <= entry.TotalOperationCount + entry.Tolerance;
                rows.Add(new InteropCountComparisonRow(
                    result.Name,
                    result.TotalOperationCount,
                    entry.TotalOperationCount,
                    entry.Tolerance,
                    HasBaseline: true,
                    WithinBaseline: within));
            }
            else
            {
                rows.Add(new InteropCountComparisonRow(
                    result.Name,
                    result.TotalOperationCount,
                    BaselineTotal: null,
                    Tolerance: 0,
                    HasBaseline: false,
                    WithinBaseline: false));
            }
        }
        return new InteropCountComparison(rows);
    }
}
