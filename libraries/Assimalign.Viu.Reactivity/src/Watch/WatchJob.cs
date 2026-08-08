using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A watcher's deferred reaction handed to an <see cref="IReactiveWatchScheduler"/> — the unit the runtime's
/// flush queue runs for a <see cref="WatchFlushMode.Pre"/> or <see cref="WatchFlushMode.Post"/>
/// watcher. Created by the reactivity layer; the scheduler reads <see cref="Flush"/> to pick the
/// phase, calls <see cref="Invoke"/> to run the reaction, and honors <see cref="IsActive"/> so a
/// stopped watcher's queued job becomes a no-op. Specified by <c>[RCT-12]</c>.
/// </summary>
public sealed class WatchJob
{
    private readonly Action _run;

    /// <summary>
    /// Creates a synchronous standalone job with host-relative ordering metadata. Runtime-created
    /// watcher jobs use their requested pre- or post-flush phase instead.
    /// </summary>
    /// <param name="sequence">The host-relative ordering sequence.</param>
    /// <param name="callback">The callback invoked while the job remains active.</param>
    public WatchJob(long sequence, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Sequence = sequence;
        _run = callback;
        Flush = WatchFlushMode.Synchronous;
    }

    internal WatchJob(Action run, WatchFlushMode flush)
    {
        ArgumentNullException.ThrowIfNull(run);
        _run = run;
        Flush = flush;
    }

    /// <summary>Gets the host-relative ordering sequence supplied for a standalone job.</summary>
    public long Sequence { get; }

    /// <summary>The flush phase this job belongs to (<see cref="WatchFlushMode.Pre"/> or <see cref="WatchFlushMode.Post"/>).</summary>
    public WatchFlushMode Flush { get; }

    /// <summary>Whether the owning watcher is still running; a stopped watcher sets this <see langword="false"/>.</summary>
    public bool IsActive { get; internal set; } = true;

    /// <summary>Runs the watcher's reaction, or does nothing when the watcher has stopped.</summary>
    public void Invoke()
    {
        if (IsActive)
        {
            _run();
        }
    }

    /// <summary>Runs the active job once; equivalent to <see cref="Invoke"/>.</summary>
    public void Run() => Invoke();
}
