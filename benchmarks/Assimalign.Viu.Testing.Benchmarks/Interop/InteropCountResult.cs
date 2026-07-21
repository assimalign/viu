using System;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The node operations one scenario's measured step performed, read from the in-memory renderer's op
/// log. <see cref="TotalOperationCount"/> is the crossing metric the gate compares to the baseline (in
/// the browser each of these node ops is a JSImport call, per the epic's "N node ops == N interop calls"
/// equivalence); the per-kind counts explain a total in the report. A plain data record so it round-trips
/// through the results JSON via source generation.
/// </summary>
public sealed class InteropCountResult
{
    /// <summary>The scenario id this result belongs to.</summary>
    public string Name { get; init; } = "";

    /// <summary>Every node operation the measured step logged — the interop-crossing proxy.</summary>
    public int TotalOperationCount { get; init; }

    /// <summary>The structural operations (inserts plus removes).</summary>
    public int StructuralOperationCount { get; init; }

    /// <summary>Elements created.</summary>
    public int CreateElementCount { get; init; }

    /// <summary>Text nodes created.</summary>
    public int CreateTextCount { get; init; }

    /// <summary>Comment nodes created.</summary>
    public int CreateCommentCount { get; init; }

    /// <summary>Element text-content replacements.</summary>
    public int SetElementTextCount { get; init; }

    /// <summary>Text-node content updates.</summary>
    public int SetTextCount { get; init; }

    /// <summary>Single-property patches.</summary>
    public int PatchPropertyCount { get; init; }

    /// <summary>Node insertions.</summary>
    public int InsertCount { get; init; }

    /// <summary>Node removals.</summary>
    public int RemoveCount { get; init; }

    /// <summary>Raw static-markup insertions.</summary>
    public int InsertStaticContentCount { get; init; }

    /// <summary>Reads the counts for <paramref name="name"/> out of <paramref name="log"/>.</summary>
    /// <param name="name">The scenario id.</param>
    /// <param name="log">The op log after the measured step.</param>
    /// <returns>The populated result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="log"/> is null.</exception>
    public static InteropCountResult From(string name, TestNodeOperationLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return new InteropCountResult
        {
            Name = name,
            TotalOperationCount = log.Operations.Count,
            StructuralOperationCount = log.StructuralOperationCount,
            CreateElementCount = log.Count(TestNodeOperationType.CreateElement),
            CreateTextCount = log.Count(TestNodeOperationType.CreateText),
            CreateCommentCount = log.Count(TestNodeOperationType.CreateComment),
            SetElementTextCount = log.Count(TestNodeOperationType.SetElementText),
            SetTextCount = log.Count(TestNodeOperationType.SetText),
            PatchPropertyCount = log.Count(TestNodeOperationType.PatchProperty),
            InsertCount = log.Count(TestNodeOperationType.Insert),
            RemoveCount = log.Count(TestNodeOperationType.Remove),
            InsertStaticContentCount = log.Count(TestNodeOperationType.InsertStaticContent),
        };
    }
}
