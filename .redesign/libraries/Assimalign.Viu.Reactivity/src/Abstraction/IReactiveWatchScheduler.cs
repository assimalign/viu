namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Schedules pre-flush and post-flush watcher jobs without coupling Reactivity to a renderer.
/// Standalone watchers fall back to synchronous delivery when no scheduler is supplied. Specified
/// by <c>[RCT-12]</c>.
/// </summary>
public interface IReactiveWatchScheduler
{
    /// <summary>
    /// Queues <paramref name="job"/> in its requested flush phase. Implementations must deduplicate
    /// an already queued job and skip inactive jobs.
    /// </summary>
    /// <param name="job">The watcher reaction to schedule.</param>
    void Schedule(WatchJob job);
}
