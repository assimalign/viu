using System;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// A unit of work on the <see cref="Scheduler"/> queue — the C# port of <c>SchedulerJob</c> in
/// <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/scheduler.ts</c>). Jobs are
/// deduplicated by instance while queued, ordered by <see cref="Identifier"/> (a component's uid,
/// so parents update before children), and phase-ordered by <see cref="IsPreFlush"/>.
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class SchedulerJob
{
    private readonly Action _callback;

    /// <summary>Creates a job over <paramref name="callback"/>.</summary>
    /// <param name="callback">The work to run when the queue flushes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public SchedulerJob(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
    }

    /// <summary>
    /// The ordering id (upstream: <c>job.id</c>) — a component's uid, so a parent (lower uid)
    /// updates before its children. Null sorts after every numbered job (upstream treats a
    /// missing id as infinity).
    /// </summary>
    public int? Identifier { get; init; }

    /// <summary>A diagnostic name used when reporting recursive-update errors.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether the job runs in the pre-flush phase (watcher callbacks), before render jobs of
    /// the same id (upstream: <c>SchedulerJobFlags.PRE</c>).
    /// </summary>
    public bool IsPreFlush
    {
        get => (Flags & SchedulerJobFlags.PreFlush) != 0;
        init => Flags = value ? Flags | SchedulerJobFlags.PreFlush : Flags & ~SchedulerJobFlags.PreFlush;
    }

    /// <summary>
    /// Whether the job may re-queue itself while it is running (upstream:
    /// <c>SchedulerJobFlags.ALLOW_RECURSE</c>). The <see cref="Scheduler"/> recursion limit
    /// still applies.
    /// </summary>
    public bool AllowRecurse
    {
        get => (Flags & SchedulerJobFlags.AllowRecurse) != 0;
        set => Flags = value ? Flags | SchedulerJobFlags.AllowRecurse : Flags & ~SchedulerJobFlags.AllowRecurse;
    }

    /// <summary>
    /// Marks the job's owner as torn down (upstream: <c>SchedulerJobFlags.DISPOSED</c>); a
    /// queued-but-disposed job is skipped by the flush.
    /// </summary>
    public bool IsDisposed
    {
        get => (Flags & SchedulerJobFlags.Disposed) != 0;
        set => Flags = value ? Flags | SchedulerJobFlags.Disposed : Flags & ~SchedulerJobFlags.Disposed;
    }

    internal SchedulerJobFlags Flags;

    internal int ExecutionsInCurrentFlushCycle;

    // Queue-assigned tiebreak so equal-id post-flush callbacks keep insertion order (JS array
    // sort is spec-stable; List<T>.Sort is not — child-before-parent Mounted ordering depends
    // on this).
    internal long InsertionSequence;

    /// <summary>
    /// The sort key: <see cref="Identifier"/> (null last) with pre-flush jobs ordered before
    /// render jobs of the same id, mirroring upstream <c>findInsertionIndex</c>.
    /// </summary>
    internal long OrderKey
        => ((long)(Identifier ?? int.MaxValue) << 1) | (IsPreFlush ? 0L : 1L);

    internal void Invoke() => _callback();
}
