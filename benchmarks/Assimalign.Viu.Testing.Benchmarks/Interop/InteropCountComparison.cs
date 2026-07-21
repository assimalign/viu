using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The outcome of comparing a run's measured interop counts to the reviewed baseline. The gate
/// <see cref="Passed"/> only when no scenario exceeds its ceiling <b>and</b> every measured scenario has
/// a baseline entry — a new scenario without a reviewed ceiling is a protection gap, not a silent pass.
/// </summary>
public sealed class InteropCountComparison
{
    /// <summary>Builds the comparison from its rows.</summary>
    /// <param name="rows">The per-scenario comparison rows.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public InteropCountComparison(IReadOnlyList<InteropCountComparisonRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Rows = rows;
        var regressions = 0;
        var missing = 0;
        foreach (var row in rows)
        {
            if (!row.HasBaseline)
            {
                missing++;
            }
            else if (!row.WithinBaseline)
            {
                regressions++;
            }
        }
        RegressionCount = regressions;
        MissingBaselineCount = missing;
    }

    /// <summary>The per-scenario comparison rows.</summary>
    public IReadOnlyList<InteropCountComparisonRow> Rows { get; }

    /// <summary>How many scenarios exceeded their ceiling.</summary>
    public int RegressionCount { get; }

    /// <summary>How many measured scenarios had no baseline entry.</summary>
    public int MissingBaselineCount { get; }

    /// <summary>Whether the gate passes (no regressions and no missing baselines).</summary>
    public bool Passed => RegressionCount == 0 && MissingBaselineCount == 0;
}
