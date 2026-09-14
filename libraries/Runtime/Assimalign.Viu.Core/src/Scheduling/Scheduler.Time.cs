using System;
using System.ComponentModel;

namespace Assimalign.Viu;

public static partial class Scheduler
{
    /// <summary>
    /// Installs the clock used for scheduler-owned deadlines in the current execution flow.
    /// Existing deadlines keep their original clock. Dispose leases in reverse installation order.
    /// This single-threaded host-test seam makes Suspense timeouts deterministic [BLT-16].
    /// </summary>
    /// <param name="timeProvider">The clock used for subsequently scheduled deadlines.</param>
    /// <returns>A lease restoring the previous clock.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IDisposable UseTimeProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return new SchedulerTimeProviderRegistration(State, timeProvider);
    }

    internal static IDisposable ScheduleDelay(int milliseconds, Action callback) =>
        new SchedulerDelay(State.TimeProvider, milliseconds, callback);
}
