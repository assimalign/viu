using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Builds and mutates the synthetic row lists the scenarios operate on — the C# port of
/// js-framework-benchmark's data helpers (<c>buildData</c>, <c>update</c>, <c>swapRows</c>;
/// https://github.com/krausest/js-framework-benchmark/blob/master/frameworks/keyed/vanillajs/src/main.js).
/// Row ids are monotonic across a source so keys stay globally unique (a create-1000 after a clear never
/// reuses an old key), and labels are drawn from the same adjective/colour/noun word lists with a fixed
/// seed so a run is reproducible. The mutation helpers are pure — they return a new list, leaving the
/// input untouched, so a scenario keeps the "previous" tree to diff the "next" one against. Not
/// thread-safe (single-threaded benchmark host).
/// </summary>
public sealed class BenchmarkRowSource
{
    private static readonly string[] _adjectives =
    [
        "pretty", "large", "big", "small", "tall", "short", "long", "handsome", "plain", "quaint",
        "clean", "elegant", "easy", "angry", "crazy", "helpful", "mushy", "odd", "unsightly", "adorable",
        "important", "inexpensive", "cheap", "expensive", "fancy",
    ];

    private static readonly string[] _colours =
    [
        "red", "yellow", "blue", "green", "pink", "brown", "purple", "brown", "white", "black", "orange",
    ];

    private static readonly string[] _nouns =
    [
        "table", "chair", "house", "bbq", "desk", "car", "pony", "cookie", "sandwich", "burger", "pizza",
        "mouse", "keyboard",
    ];

    private readonly Random _random;
    private int _nextIdentifier = 1;

    /// <summary>Creates a source over a fixed seed so label choices (hence any run) are reproducible.</summary>
    /// <param name="seed">The random seed; the default keeps every run identical.</param>
    public BenchmarkRowSource(int seed = 8675309) => _random = new Random(seed);

    /// <summary>Builds a fresh list of <paramref name="count"/> rows with new, never-reused ids.</summary>
    /// <param name="count">The number of rows.</param>
    /// <returns>The new row list.</returns>
    public List<BenchmarkRow> Build(int count)
    {
        var rows = new List<BenchmarkRow>(count);
        Append(rows, count);
        return rows;
    }

    /// <summary>Appends <paramref name="count"/> new rows to <paramref name="rows"/> in place.</summary>
    /// <param name="rows">The list to grow.</param>
    /// <param name="count">The number of rows to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public void Append(List<BenchmarkRow> rows, int count)
    {
        ArgumentNullException.ThrowIfNull(rows);
        for (var index = 0; index < count; index++)
        {
            rows.Add(new BenchmarkRow(_nextIdentifier++, NextLabel()));
        }
    }

    /// <summary>
    /// Returns a copy of <paramref name="rows"/> with every <paramref name="step"/>-th row's label
    /// suffixed with <c>" !!!"</c> (js-framework-benchmark's "update every 10th row" with step 10) —
    /// keys are unchanged, so the keyed diff patches only those labels' text.
    /// </summary>
    /// <param name="rows">The rows to update.</param>
    /// <param name="step">The stride (10 for the canonical scenario).</param>
    /// <returns>The updated copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is not positive.</exception>
    public static List<BenchmarkRow> UpdateEvery(IReadOnlyList<BenchmarkRow> rows, int step)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        var updated = new List<BenchmarkRow>(rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            updated.Add((index % step) == 0 ? row with { Label = row.Label + " !!!" } : row);
        }
        return updated;
    }

    /// <summary>
    /// Returns a copy of <paramref name="rows"/> with the rows at <paramref name="first"/> and
    /// <paramref name="second"/> swapped (js-framework-benchmark's "swap rows", indices 1 and 998 for
    /// 1000 rows) — the two keys move, exercising the keyed diff's move path.
    /// </summary>
    /// <param name="rows">The rows to reorder.</param>
    /// <param name="first">The first index.</param>
    /// <param name="second">The second index.</param>
    /// <returns>The reordered copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public static List<BenchmarkRow> Swap(IReadOnlyList<BenchmarkRow> rows, int first, int second)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfNegative(second);
        var swapped = new List<BenchmarkRow>(rows);
        (swapped[first], swapped[second]) = (swapped[second], swapped[first]);
        return swapped;
    }

    /// <summary>
    /// Returns a copy of <paramref name="rows"/> with the row at <paramref name="index"/> removed
    /// (js-framework-benchmark's "remove row") — the keyed diff removes exactly that node, but a
    /// keyless diff must re-patch every row after it.
    /// </summary>
    /// <param name="rows">The rows to shrink.</param>
    /// <param name="index">The index to remove.</param>
    /// <returns>The shrunken copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public static List<BenchmarkRow> RemoveAt(IReadOnlyList<BenchmarkRow> rows, int index)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var remaining = new List<BenchmarkRow>(rows);
        remaining.RemoveAt(index);
        return remaining;
    }

    private string NextLabel()
        => string.Concat(
            _adjectives[_random.Next(_adjectives.Length)],
            " ",
            _colours[_random.Next(_colours.Length)],
            " ",
            _nouns[_random.Next(_nouns.Length)]);
}
