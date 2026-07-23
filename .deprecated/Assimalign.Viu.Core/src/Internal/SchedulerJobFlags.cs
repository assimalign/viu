using System;

namespace Assimalign.Viu;

/// <summary>
/// Internal state bits for a <see cref="SchedulerJob"/>, mirroring <c>SchedulerJobFlags</c> in
/// <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/scheduler.ts</c>). The public knobs
/// (<see cref="SchedulerJob.IsPreFlush"/>, <see cref="SchedulerJob.AllowRecurse"/>,
/// <see cref="SchedulerJob.IsDisposed"/>) surface the stable bits; <see cref="Queued"/> is
/// queue-internal bookkeeping.
/// </summary>
[Flags]
internal enum SchedulerJobFlags
{
    /// <summary>The job is currently in the queue (upstream: <c>QUEUED</c>).</summary>
    Queued = 1,

    /// <summary>The job runs in the pre-flush phase, before render jobs (upstream: <c>PRE</c>).</summary>
    PreFlush = 1 << 1,

    /// <summary>
    /// The job may re-queue itself while running (upstream: <c>ALLOW_RECURSE</c>); the
    /// recursion limit still applies.
    /// </summary>
    AllowRecurse = 1 << 2,

    /// <summary>The job's owner was torn down; the flush skips it (upstream: <c>DISPOSED</c>).</summary>
    Disposed = 1 << 3,
}
