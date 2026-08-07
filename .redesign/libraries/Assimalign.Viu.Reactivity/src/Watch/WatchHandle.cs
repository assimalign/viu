using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The handle returned by <c>Watch</c> and <c>WatchEffect</c>. Stops the watcher,
/// or pauses and resumes callback delivery. Watchers created inside an <see cref="EffectScope"/>
/// also stop when the scope stops, so an explicit <see cref="Stop"/> is only needed for
/// independently created watchers. Implements <see cref="IDisposable"/> for <c>using</c> support.
/// Specified by <c>[RCT-5]</c>, <c>[RCT-10]</c>, and <c>[RCT-12]</c>.
/// </summary>
public sealed class WatchHandle : IDisposable
{
    private readonly Watcher? _watcher;
    private bool _isStandaloneActive = true;

    /// <summary>
    /// Creates a standalone handle with no watcher attached. This preserves the scaffold contract
    /// for hosts that need an independently controlled lifetime token; production watches return
    /// handles attached to their internal watcher.
    /// </summary>
    public WatchHandle()
    {
    }

    internal WatchHandle(Watcher watcher) => _watcher = watcher;

    /// <summary>Whether the watcher is still running.</summary>
    public bool IsActive => _watcher?.IsActive ?? _isStandaloneActive;

    /// <summary>Stops the watcher, unlinking its dependencies and running the pending cleanup. Idempotent.</summary>
    public void Stop()
    {
        if (_watcher is null)
        {
            _isStandaloneActive = false;
        }
        else
        {
            _watcher.Stop();
        }
    }

    /// <summary>Defers callbacks until <see cref="Resume"/>; a change while paused delivers one trailing callback.</summary>
    public void Pause() => _watcher?.Pause();

    /// <summary>Resumes callback delivery paused by <see cref="Pause"/>.</summary>
    public void Resume() => _watcher?.Resume();

    /// <summary>Stops the watcher; equivalent to <see cref="Stop"/> for <c>using</c> support.</summary>
    public void Dispose() => Stop();
}
