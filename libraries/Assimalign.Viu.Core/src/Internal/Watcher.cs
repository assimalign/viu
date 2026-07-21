using System;

namespace Assimalign.Viu;

/// <summary>
/// Shared machinery behind <c>Watch</c> and <c>WatchEffect</c> — the C# port of the state Vue 3.5's
/// <c>baseWatch</c> keeps per watcher (<c>packages/reactivity/src/watch.ts</c>): the underlying
/// <see cref="ReactiveEffect"/>, the pending cleanup, the flush routing, and the stop/pause plumbing.
/// A trigger re-enters through <see cref="OnTrigger"/>, which either reacts synchronously or hands a
/// <see cref="WatchJob"/> to the injected <see cref="IWatchScheduler"/> for pre/post timing. Teardown
/// funnels through the effect's <see cref="ReactiveEffect.OnStop"/> so stopping the owning
/// <see cref="EffectScope"/> also runs the watcher's cleanup. Not thread-safe (single-threaded JS
/// event-loop model).
/// </summary>
internal abstract class Watcher
{
    private readonly WatchFlushMode _flush;
    private readonly IWatchScheduler? _scheduler;
    private readonly bool _once;
    private Action? _cleanup;
    private WatchJob? _job;
    private bool _active = true;

    /// <summary>The tracking effect; assigned by the subclass before <see cref="Initialize"/>.</summary>
    protected ReactiveEffect Effect = null!;

    /// <summary>Cached <c>onCleanup</c> delegate handed to callbacks and effect bodies.</summary>
    protected readonly OnCleanup RegisterCleanup;

    private protected Watcher(WatchFlushMode flush, IWatchScheduler? scheduler, bool once)
    {
        _flush = flush;
        _scheduler = scheduler;
        _once = once;
        RegisterCleanup = Register;
    }

    /// <summary>Whether the watcher is still running.</summary>
    internal bool IsActive => _active;

    /// <summary>Stops the watcher: unlinks dependencies and runs the pending cleanup. Idempotent.</summary>
    internal void Stop() => Effect.Stop();

    /// <summary>Defers callbacks until <see cref="Resume"/> (Vue 3.5 <c>WatchHandle.pause</c>).</summary>
    internal void Pause() => Effect.Pause();

    /// <summary>Resumes callback delivery (Vue 3.5 <c>WatchHandle.resume</c>).</summary>
    internal void Resume() => Effect.Resume();

    /// <summary>
    /// Wires the effect's scheduler and stop hooks. The subclass calls this after building
    /// <see cref="Effect"/> and before its first run.
    /// </summary>
    protected void Initialize()
    {
        Effect.Scheduler = OnTrigger;
        Effect.OnStop = OnEffectStopped;
    }

    /// <summary>Re-runs the source/effect and delivers the callback; defined by the concrete watcher.</summary>
    protected abstract void React();

    /// <summary>Stops the watcher after a delivered callback when <c>Once</c> was requested.</summary>
    protected void AfterCallback()
    {
        if (_once)
        {
            Stop();
        }
    }

    /// <summary>Runs and clears the registered cleanup(s); a no-op when none are pending.</summary>
    protected void RunCleanup()
    {
        var cleanup = _cleanup;
        if (cleanup is null)
        {
            return;
        }
        _cleanup = null;
        cleanup();
    }

    private void OnTrigger()
    {
        if (!_active)
        {
            return;
        }
        if (_flush == WatchFlushMode.Sync || _scheduler is null)
        {
            React();
            return;
        }
        _job ??= new WatchJob(React, _flush);
        _scheduler.Schedule(_job);
    }

    private void Register(Action cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        _cleanup += cleanup;
    }

    private void OnEffectStopped()
    {
        _active = false;
        if (_job is not null)
        {
            _job.IsActive = false;
        }
        RunCleanup();
    }
}
