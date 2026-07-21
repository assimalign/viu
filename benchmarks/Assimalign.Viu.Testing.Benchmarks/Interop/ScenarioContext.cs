using System;
using System.Collections.Generic;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The per-run state a scenario mutates — one in-memory renderer, its container, a deterministic row
/// source, and the current row list. A scenario's prepare step mounts a baseline tree through
/// <see cref="Render"/>; its measured step renders the next tree, which the renderer diffs against the
/// mounted one, landing every node operation in the renderer's op log (the interop-crossing proxy).
/// Not thread-safe (single-threaded benchmark host).
/// </summary>
public sealed class ScenarioContext
{
    /// <summary>Creates a context over a fresh renderer, container, and (seeded) row source.</summary>
    /// <param name="renderer">The in-memory renderer whose op log is measured.</param>
    /// <param name="variant">The tree shape the scenario renders.</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is null.</exception>
    public ScenarioContext(TestRenderer renderer, ScenarioVariant variant)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        Renderer = renderer;
        Variant = variant;
        Container = renderer.CreateContainer();
        Source = new BenchmarkRowSource();
        Rows = [];
    }

    /// <summary>The renderer under measurement.</summary>
    public TestRenderer Renderer { get; }

    /// <summary>The container the scenario renders into.</summary>
    public TestElement Container { get; }

    /// <summary>The tree shape (keyed+flagged, or the keyless bypass).</summary>
    public ScenarioVariant Variant { get; }

    /// <summary>The deterministic row source for this run (fresh per context, so runs are reproducible).</summary>
    public BenchmarkRowSource Source { get; }

    /// <summary>The currently mounted row list.</summary>
    public IReadOnlyList<BenchmarkRow> Rows { get; private set; }

    /// <summary>The selected row id applied on the next <see cref="Render"/>, or null for none.</summary>
    public int? SelectedIdentifier { get; set; }

    /// <summary>
    /// Renders <paramref name="rows"/> (with the current <see cref="SelectedIdentifier"/> and
    /// <see cref="Variant"/>) into the container, diffing against whatever is mounted.
    /// </summary>
    /// <param name="rows">The rows to render.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public void Render(IReadOnlyList<BenchmarkRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Rows = rows;
        Renderer.Render(RowTableBuilder.Build(rows, SelectedIdentifier, Variant), Container);
    }
}
