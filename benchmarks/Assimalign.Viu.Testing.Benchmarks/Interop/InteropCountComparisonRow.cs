namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// One scenario's line in the gate comparison: the measured crossings against the reviewed ceiling.
/// </summary>
/// <param name="Name">The scenario id.</param>
/// <param name="MeasuredTotal">The crossings the measured step performed.</param>
/// <param name="BaselineTotal">The reviewed ceiling, or null when the scenario has no baseline entry.</param>
/// <param name="Tolerance">The permitted increase over the ceiling.</param>
/// <param name="HasBaseline">Whether a baseline entry existed for the scenario.</param>
/// <param name="WithinBaseline">Whether the measurement is at or under <c>ceiling + tolerance</c>.</param>
public readonly record struct InteropCountComparisonRow(
    string Name,
    int MeasuredTotal,
    int? BaselineTotal,
    int Tolerance,
    bool HasBaseline,
    bool WithinBaseline)
{
    /// <summary>The signed change from the ceiling, or null when there is no baseline entry.</summary>
    public int? Delta => BaselineTotal is int baseline ? MeasuredTotal - baseline : null;
}
