namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Schedules a watch job according to a host's flush and ordering policy.
/// </summary>
/// <remarks>
/// This is a genuine host port: standalone execution, Core's phased scheduler, and deterministic
/// tests provide meaningfully different policies.
/// </remarks>
public interface IReactiveWatchScheduler
{
    /// <summary>Schedules a stable job for later or immediate execution.</summary>
    /// <param name="job">The job identity and callback.</param>
    void Schedule(WatchJob job);
}
