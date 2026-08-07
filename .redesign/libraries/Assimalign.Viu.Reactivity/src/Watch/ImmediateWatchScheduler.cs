using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Executes scheduled watch jobs synchronously for standalone composition and small tests.
/// </summary>
public sealed class ImmediateWatchScheduler : IReactiveWatchScheduler
{
    /// <inheritdoc />
    public void Schedule(WatchJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        job.Run();
    }
}
