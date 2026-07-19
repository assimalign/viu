namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The seam through which a <see cref="WatchFlushMode.Pre"/> or <see cref="WatchFlushMode.Post"/>
/// watcher hands its work to the runtime's flush queue. The reactivity layer must not reference the
/// runtime scheduler (dependency direction), so a watcher with pre/post timing calls
/// <see cref="Schedule"/> instead of running inline — exactly as <see cref="ReactiveEffect.Scheduler"/>
/// defers a render. The runtime (<c>[V01.01.03.04]</c>) binds an implementation that routes
/// <see cref="WatchJob.Flush"/> to the pre-flush queue or post-flush callbacks; standalone reactivity
/// runs synchronously with no scheduler.
/// </summary>
public interface IWatchScheduler
{
    /// <summary>
    /// Queues <paramref name="job"/> to run in its <see cref="WatchJob.Flush"/> phase. Implementations
    /// deduplicate a job already queued for the same flush and must skip a job that is no longer
    /// <see cref="WatchJob.IsActive"/>.
    /// </summary>
    /// <param name="job">The watcher reaction to schedule.</param>
    void Schedule(WatchJob job);
}
