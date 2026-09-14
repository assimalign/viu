namespace Assimalign.Viu;

/// <summary>Observes the boundaries of complete scheduler flush chains.</summary>
/// <remarks>
/// Implement this optional contract beside <see cref="IRuntimeInspectionHook"/> to receive
/// allocation-free value payloads through the same registration. Hooks must enqueue and return
/// immediately without scheduling or executing application work. Failures are isolated and
/// dependency collection is suspended during observation. Specified by <c>[DVT-8]</c> and
/// <c>[DVT-10]</c>.
/// </remarks>
public interface IRuntimeSchedulerInspectionHook
{
    /// <summary>Observes the start of a flush chain before its first job or commit.</summary>
    /// <param name="flush">The chain identity with zero execution counts.</param>
    void FlushStarted(in RuntimeInspectionFlush flush);

    /// <summary>Observes the end of a flush chain before its next-tick task completes.</summary>
    /// <param name="flush">The same identity and actual phase invocation counts, including failures.</param>
    void FlushCompleted(in RuntimeInspectionFlush flush);
}
