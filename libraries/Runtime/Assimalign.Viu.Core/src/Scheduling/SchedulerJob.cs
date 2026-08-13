using System;

namespace Assimalign.Viu;

/// <summary>
/// Represents one scheduler job, deduplicated by instance and ordered with its mounted component.
/// </summary>
/// <remarks>
/// Jobs are single-threaded runtime objects. Their ordering and recursion behavior are specified by
/// <c>[SCH-2]</c> through <c>[SCH-8]</c>.
/// </remarks>
public sealed class SchedulerJob
{
    private readonly Action _callback;

    /// <summary>Initializes a job over the callback invoked when its flush phase runs.</summary>
    /// <param name="callback">The callback to invoke.</param>
    public SchedulerJob(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
    }

    /// <summary>
    /// Gets the optional mounted-component identifier used for parent-before-child ordering.
    /// Identifier-less render jobs sort after every identified job.
    /// </summary>
    public int? Identifier { get; init; }

    /// <summary>Gets an optional diagnostic name used in recursive-update errors.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets whether this job belongs to the pre-flush watcher phase. A pre-flush job sorts before
    /// the render job with the same identifier. Specified by <c>[SCH-2]</c> and <c>[SCH-3]</c>.
    /// </summary>
    public bool IsPreFlush
    {
        get => (Flags & SchedulerJobFlags.PreFlush) != 0;
        init => Flags = value
            ? Flags | SchedulerJobFlags.PreFlush
            : Flags & ~SchedulerJobFlags.PreFlush;
    }

    /// <summary>
    /// Gets or sets whether the job may queue itself while running. The per-flush recursion limit
    /// still applies. Specified by <c>[SCH-5]</c> and <c>[SCH-8]</c>.
    /// </summary>
    public bool AllowRecurse
    {
        get => (Flags & SchedulerJobFlags.AllowRecurse) != 0;
        set => Flags = value
            ? Flags | SchedulerJobFlags.AllowRecurse
            : Flags & ~SchedulerJobFlags.AllowRecurse;
    }

    /// <summary>
    /// Gets or sets whether teardown has invalidated this job. A queued disposed job is skipped.
    /// </summary>
    public bool IsDisposed
    {
        get => (Flags & SchedulerJobFlags.Disposed) != 0;
        set => Flags = value
            ? Flags | SchedulerJobFlags.Disposed
            : Flags & ~SchedulerJobFlags.Disposed;
    }

    internal SchedulerJobFlags Flags;

    internal int ExecutionsInCurrentFlushChain;

    internal long InsertionSequence;

    internal long OrderKey =>
        ((long)(Identifier ?? (IsPreFlush ? -1 : int.MaxValue)) << 1)
        | (IsPreFlush ? 0L : 1L);

    internal void Invoke() => _callback();
}
