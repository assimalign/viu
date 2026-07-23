using System;
using System.Diagnostics;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The subscriber primitive underneath render effects and watchers — the C# port of Vue 3.5's
/// <c>ReactiveEffect</c>. <see cref="Run"/> executes the function with this effect installed as
/// the ambient active subscriber, re-collecting dependencies with version-based cleanup (deps not
/// read in the latest run are unlinked). When a tracked dependency triggers, the effect either
/// invokes its <see cref="Scheduler"/> (exactly once per batch) or re-runs synchronously.
/// Not thread-safe: designed for the single-threaded JS event-loop model.
/// </summary>
public sealed class ReactiveEffect : Subscriber
{
    private readonly Action _function;
    private bool _pendingWhilePaused;

    /// <summary>
    /// Creates an effect over <paramref name="function"/>. The effect does not run until
    /// <see cref="Run"/> is called. If an <see cref="EffectScope"/> is active, the effect
    /// registers with it and is stopped when the scope stops.
    /// </summary>
    /// <param name="function">The reactive function to track.</param>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is null.</exception>
    public ReactiveEffect(Action function)
    {
        ArgumentNullException.ThrowIfNull(function);
        _function = function;
        Flags = SubscriberFlags.Active | SubscriberFlags.Tracking;
        EffectScope.Current?.RegisterEffect(this);
    }

    /// <summary>
    /// Optional scheduler invoked when a dependency triggers, instead of re-running the effect
    /// inline. Called exactly once per batch; call <see cref="Run"/> to re-execute.
    /// </summary>
    public Action? Scheduler { get; set; }

    /// <summary>Callback invoked exactly once when the effect is stopped.</summary>
    public Action? OnStop { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the effect may re-trigger itself by writing its own
    /// dependencies (Vue <c>ALLOW_RECURSE</c> parity). Default <see langword="false"/>:
    /// self-triggering while running is ignored, preventing infinite loops.
    /// </summary>
    public bool AllowRecurse
    {
        get => (Flags & SubscriberFlags.AllowRecurse) != 0;
        set
        {
            if (value)
            {
                Flags |= SubscriberFlags.AllowRecurse;
            }
            else
            {
                Flags &= ~SubscriberFlags.AllowRecurse;
            }
        }
    }

    /// <summary>Whether the effect has not been stopped.</summary>
    public bool IsActive => (Flags & SubscriberFlags.Active) != 0;

    /// <summary>
    /// Executes the function with this effect as the active subscriber, re-collecting its
    /// dependencies. After a stop, runs the function untracked (Vue parity). The previous active
    /// subscriber is restored even if the function throws, so nested effects stay isolated.
    /// </summary>
    public void Run()
    {
        if ((Flags & SubscriberFlags.Active) == 0)
        {
            _function();
            return;
        }
        Flags |= SubscriberFlags.Running;
        SubscriberOperations.PrepareDependencies(this);
        var previousSubscriber = ReactivityState.ActiveSubscriber;
        var previousShouldTrack = ReactivityState.ShouldTrack;
        ReactivityState.ActiveSubscriber = this;
        ReactivityState.ShouldTrack = true;
        try
        {
            _function();
        }
        finally
        {
            Debug.Assert(ReferenceEquals(ReactivityState.ActiveSubscriber, this), "Active subscriber stack corrupted.");
            SubscriberOperations.CleanupDependencies(this);
            ReactivityState.ActiveSubscriber = previousSubscriber;
            ReactivityState.ShouldTrack = previousShouldTrack;
            Flags &= ~SubscriberFlags.Running;
        }
    }

    /// <summary>
    /// Unlinks every dependency, invokes <see cref="OnStop"/>, and suppresses all future
    /// notifications. Idempotent. A later <see cref="Run"/> executes the function untracked.
    /// </summary>
    public void Stop()
    {
        if ((Flags & SubscriberFlags.Active) == 0)
        {
            return;
        }
        for (var link = Dependencies; link is not null; link = link.NextDependency)
        {
            SubscriberOperations.RemoveSubscriber(link);
        }
        Dependencies = DependenciesTail = null;
        Flags &= ~SubscriberFlags.Active;
        OnStop?.Invoke();
    }

    /// <summary>Defers invalidations: while paused, triggers are remembered but not delivered.</summary>
    public void Pause() => Flags |= SubscriberFlags.Paused;

    /// <summary>
    /// Resumes notification delivery; if anything triggered while paused, delivers a single
    /// trailing invalidation (Vue 3.5 pause/resume parity).
    /// </summary>
    public void Resume()
    {
        if ((Flags & SubscriberFlags.Paused) != 0)
        {
            Flags &= ~SubscriberFlags.Paused;
            if (_pendingWhilePaused)
            {
                _pendingWhilePaused = false;
                TriggerInvalidation();
            }
        }
    }

    /// <summary>Re-runs the effect only if a tracked dependency actually changed.</summary>
    public void RunIfDirty()
    {
        if (SubscriberOperations.IsDirty(this))
        {
            Run();
        }
    }

    /// <summary>Batch-flush entry: routes to the paused queue, the scheduler, or a dirty-checked run.</summary>
    internal void TriggerInvalidation()
    {
        if ((Flags & SubscriberFlags.Paused) != 0)
        {
            _pendingWhilePaused = true;
        }
        else if (Scheduler is not null)
        {
            Scheduler();
        }
        else
        {
            RunIfDirty();
        }
    }

    /// <summary>
    /// Called when a tracked dependency triggers: queues this effect into the current batch unless a
    /// recursion guard suppresses it. Always returns <see langword="false"/> — effects have no
    /// readers to propagate to.
    /// </summary>
    internal override bool Notify()
    {
        // Recursion guard: an effect writing its own dependency does not re-enter
        // unless AllowRecurse is set.
        if ((Flags & SubscriberFlags.Running) != 0 && (Flags & SubscriberFlags.AllowRecurse) == 0)
        {
            return false;
        }
        if ((Flags & SubscriberFlags.Notified) == 0)
        {
            ReactivityState.Batch(this, isComputed: false);
        }
        return false;
    }
}

