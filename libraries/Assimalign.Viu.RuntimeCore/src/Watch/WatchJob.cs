using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A watcher's deferred reaction handed to an <see cref="IWatchScheduler"/> — the unit the runtime's
/// flush queue runs for a <see cref="WatchFlushMode.Pre"/> or <see cref="WatchFlushMode.Post"/>
/// watcher. Created by the reactivity layer; the scheduler reads <see cref="Flush"/> to pick the
/// phase, calls <see cref="Invoke"/> to run the reaction, and honors <see cref="IsActive"/> so a
/// stopped watcher's queued job becomes a no-op.
/// </summary>
public sealed class WatchJob
{
    private readonly Action _run;

    internal WatchJob(Action run, WatchFlushMode flush)
    {
        _run = run;
        Flush = flush;
    }

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
}
