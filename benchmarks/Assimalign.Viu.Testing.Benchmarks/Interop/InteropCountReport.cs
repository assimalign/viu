using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Renders the interop-count comparison as a human-readable table (for CI logs and local runs) and
/// persists the machine-readable results — the interop-crossing analogue of the publish-size budget
/// report. The table is deliberately shaped like that report so the two gates read the same way.
/// </summary>
public static class InteropCountReport
{
    /// <summary>Writes the comparison table for <paramref name="results"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="results">The measured results.</param>
    /// <param name="comparison">The comparison against the baseline.</param>
    /// <param name="variant">The measured variant (labels the report).</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static void WriteComparison(
        TextWriter writer,
        IReadOnlyList<InteropCountResult> results,
        InteropCountComparison comparison,
        ScenarioVariant variant)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(comparison);

        writer.WriteLine();
        writer.WriteLine(FormattableString.Invariant($"Viu interop-count report (variant={variant})"));
        writer.WriteLine("Each count is a node operation == one JS-interop crossing in the browser.");
        writer.WriteLine(new string('=', 78));
        var header = string.Format(
            CultureInfo.InvariantCulture,
            "{0,-20} {1,10} {2,12} {3,10} {4,9}  {5}",
            "Scenario", "Measured", "Structural", "Baseline", "Delta", "Status");
        writer.WriteLine(header);
        writer.WriteLine(new string('-', header.Length));

        var structuralByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            structuralByName[result.Name] = result.StructuralOperationCount;
        }

        foreach (var row in comparison.Rows)
        {
            var structural = structuralByName.TryGetValue(row.Name, out var value) ? value : 0;
            var baselineText = row.BaselineTotal is int baseline
                ? baseline.ToString(CultureInfo.InvariantCulture)
                : "n/a";
            var deltaText = row.Delta is int delta
                ? (delta >= 0 ? "+" : "") + delta.ToString(CultureInfo.InvariantCulture)
                : "n/a";
            var status = !row.HasBaseline ? "NO BASELINE" : row.WithinBaseline ? "PASS" : "REGRESSION";
            writer.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-20} {1,10} {2,12} {3,10} {4,9}  {5}",
                row.Name,
                row.MeasuredTotal,
                structural,
                baselineText,
                deltaText,
                status));
        }

        writer.WriteLine(new string('-', header.Length));
        var passedCount = comparison.Rows.Count - comparison.RegressionCount - comparison.MissingBaselineCount;
        writer.WriteLine(FormattableString.Invariant(
            $"Result: {(comparison.Passed ? "PASS" : "FAIL")} ({passedCount}/{comparison.Rows.Count} within baseline)"));
        if (!comparison.Passed)
        {
            writer.WriteLine();
            if (comparison.RegressionCount > 0)
            {
                writer.WriteLine("A scenario crossed the interop boundary more than its reviewed baseline allows.");
                writer.WriteLine("This is a deliberate-decision gate: reduce crossings, or raise the ceiling in");
                writer.WriteLine("benchmarks/baselines/InteropCounts.json as a reviewed change (no silent ratcheting).");
            }
            if (comparison.MissingBaselineCount > 0)
            {
                writer.WriteLine("A measured scenario has no baseline entry. Add its reviewed ceiling to");
                writer.WriteLine("benchmarks/baselines/InteropCounts.json so the gate protects it.");
            }
        }
    }

    /// <summary>Builds the persisted results document.</summary>
    /// <param name="results">The measured results.</param>
    /// <param name="variant">The measured variant.</param>
    /// <param name="withinBaseline">The gate outcome, or null when measured without a baseline.</param>
    /// <returns>The results document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    public static InteropCountResultsDocument BuildDocument(
        IReadOnlyList<InteropCountResult> results,
        ScenarioVariant variant,
        bool? withinBaseline)
    {
        ArgumentNullException.ThrowIfNull(results);
        var scenarios = new InteropCountResult[results.Count];
        for (var index = 0; index < results.Count; index++)
        {
            scenarios[index] = results[index];
        }
        return new InteropCountResultsDocument
        {
            GeneratedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Variant = variant.ToString(),
            WithinBaseline = withinBaseline,
            Scenarios = scenarios,
        };
    }

    /// <summary>Writes <paramref name="document"/> as JSON to <paramref name="path"/>, creating directories.</summary>
    /// <param name="path">The results path.</param>
    /// <param name="document">The document to persist.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public static void WriteResultsJson(string path, InteropCountResultsDocument document)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(document);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var json = JsonSerializer.Serialize(document, InteropCountJsonContext.Default.InteropCountResultsDocument);
        File.WriteAllText(path, json);
    }
}
