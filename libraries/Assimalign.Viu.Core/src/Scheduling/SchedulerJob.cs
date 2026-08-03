using System;

namespace Assimalign.Viu;

/// <summary>
/// A unit of work on the <see cref="Scheduler"/> queue. Jobs are
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
    /// The ordering id — a component's uid, so a parent (lower uid) updates before its children.
    /// Null sorts after every numbered job.
    /// </summary>
    public int? Identifier { get; init; }

    /// <summary>A diagnostic name used when reporting recursive-update errors.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether the job runs in the pre-flush phase (watcher callbacks), before render jobs of
    /// the same id.
    /// </summary>
    public bool IsPreFlush
    {
        get => (Flags & SchedulerJobFlags.PreFlush) != 0;
        init => Flags = value ? Flags | SchedulerJobFlags.PreFlush : Flags & ~SchedulerJobFlags.PreFlush;
    }

    /// <summary>
    /// Whether the job may re-queue itself while it is running. The <see cref="Scheduler"/>
    /// recursion limit still applies, so a genuinely runaway job is still reported rather than
    /// spinning.
    /// </summary>
    public bool AllowRecurse
    {
        get => (Flags & SchedulerJobFlags.AllowRecurse) != 0;
        set => Flags = value ? Flags | SchedulerJobFlags.AllowRecurse : Flags & ~SchedulerJobFlags.AllowRecurse;
    }

    /// <summary>
    /// Marks the job's owner as torn down; a queued-but-disposed job is skipped by the flush
    /// rather than dequeued, so teardown never has to search the queue.
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
    /// The sort key: <see cref="Identifier"/> with pre-flush jobs ordered before render jobs of
    /// the same id. An id-less job sorts last — except an id-less <em>pre-flush</em> job, which
    /// sorts first, because an instance-less pre watcher belongs to no component and so must run
    /// ahead of every render.
    /// </summary>
    internal long OrderKey
        => ((long)(Identifier ?? (IsPreFlush ? -1 : int.MaxValue)) << 1) | (IsPreFlush ? 0L : 1L);

    internal void Invoke() => _callback();
}
