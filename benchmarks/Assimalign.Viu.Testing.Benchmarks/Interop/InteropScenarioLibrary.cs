using System.Collections.Generic;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The scenario catalogue — the js-framework-benchmark canon
/// (https://github.com/krausest/js-framework-benchmark): create 1,000 and 10,000 rows, update every
/// 10th row, swap two rows, select a row, remove a row, append 1,000, replace all, and clear. This is
/// the single source both the interop-count gate and the BenchmarkDotNet renderer timings draw from, so
/// a scenario is defined once and measured two ways. Discovery is deterministic and ordered.
/// </summary>
public static class InteropScenarioLibrary
{
    /// <summary>The row count for the standard scenarios.</summary>
    public const int RowCount = 1_000;

    /// <summary>The row count for the large create scenario.</summary>
    public const int LargeRowCount = 10_000;

    /// <summary>The stride for the "update every Nth row" scenario.</summary>
    public const int UpdateStep = 10;

    private const int SwapFirstIndex = 1;
    private const int SwapSecondIndex = 998;
    private const int RemoveIndex = 1;
    private const int SelectIndex = 1;

    /// <summary>Returns the ordered scenario catalogue (variant-independent; variant is applied per measurement).</summary>
    /// <returns>The scenarios, in a stable order.</returns>
    public static IReadOnlyList<InteropScenario> Discover()
    {
        return
        [
            new InteropScenario(
                "create-1000",
                "Mount 1,000 rows into an empty container.",
                static _ => { },
                static context => context.Render(context.Source.Build(RowCount))),

            new InteropScenario(
                "create-10000",
                "Mount 10,000 rows into an empty container.",
                static _ => { },
                static context => context.Render(context.Source.Build(LargeRowCount))),

            new InteropScenario(
                "update-every-10th",
                "Append \" !!!\" to every 10th label across 1,000 mounted rows.",
                static context => context.Render(context.Source.Build(RowCount)),
                static context => context.Render(BenchmarkRowSource.UpdateEvery(context.Rows, UpdateStep))),

            new InteropScenario(
                "swap-rows",
                "Swap rows 1 and 998 of 1,000 mounted rows.",
                static context => context.Render(context.Source.Build(RowCount)),
                static context => context.Render(BenchmarkRowSource.Swap(context.Rows, SwapFirstIndex, SwapSecondIndex))),

            new InteropScenario(
                "select-row",
                "Toggle the selected class on one row of 1,000 mounted rows.",
                static context => context.Render(context.Source.Build(RowCount)),
                static context =>
                {
                    context.SelectedIdentifier = context.Rows[SelectIndex].Identifier;
                    context.Render(context.Rows);
                }),

            new InteropScenario(
                "remove-row",
                "Remove one row from near the front of 1,000 mounted rows.",
                static context => context.Render(context.Source.Build(RowCount)),
                static context => context.Render(BenchmarkRowSource.RemoveAt(context.Rows, RemoveIndex))),

            new InteropScenario(
                "append-1000",
                "Append 1,000 rows to 1,000 mounted rows.",
                static context => context.Render(context.Source.Build(RowCount)),
                static context =>
                {
                    var grown = new List<BenchmarkRow>(context.Rows);
                    context.Source.Append(grown, RowCount);
                    context.Render(grown);
                }),

            new InteropScenario(
                "replace-1000",
                "Replace 1,000 mounted rows with 1,000 freshly keyed rows.",
                static context => context.Render(context.Source.Build(RowCount)),
                static context => context.Render(context.Source.Build(RowCount))),

            new InteropScenario(
                "clear-1000",
                "Clear all 1,000 mounted rows.",
                static context => context.Render(context.Source.Build(RowCount)),
                static context => context.Render([])),
        ];
    }
}
