using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Executes scheduled watch jobs synchronously for standalone composition and small tests.
/// Specified by the synchronous fallback in <c>[RCT-12]</c>.
/// </summary>
public sealed class ImmediateWatchScheduler : IReactiveWatchScheduler
{
    /// <inheritdoc />
    public void Schedule(WatchJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        job.Invoke();
    }
}
