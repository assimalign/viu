namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Observes dependency reads, invalidations, and effect execution without subscribing to the
/// application graph. Callbacks are synchronous, non-blocking, and single-threaded. Payload
/// objects are borrowed for the callback only; implementations must not retain them. Callback
/// failures are isolated and nested observation is suppressed. Specified by <c>[RCT-13]</c>
/// and <c>[DVT-8]</c>.
/// </summary>
public interface IReactivityInspectionHook
{
    /// <summary>Observes a tracked dependency read in evaluation order. Specified by <c>[DVT-8]</c>.</summary>
    void DependencyTracked(in ReactivityInspectionDependency dependency);

    /// <summary>Observes a write or computed invalidation before subscriber notification. Specified by <c>[DVT-8]</c>.</summary>
    void DependencyTriggered(in ReactivityInspectionDependency dependency);

    /// <summary>Observes an effect's first accepted invalidation in a batch, with its cause. Specified by <c>[DVT-8]</c>.</summary>
    void EffectScheduled(in ReactivityInspectionEffect effect);

    /// <summary>Observes an effect invocation, including an explicit run after stopping. Specified by <c>[DVT-8]</c>.</summary>
    void EffectRunStarted(in ReactivityInspectionEffect effect);

    /// <summary>Observes completion after dependency cleanup, including failed runs. Specified by <c>[DVT-8]</c>.</summary>
    void EffectRunCompleted(in ReactivityInspectionEffect effect);
}
