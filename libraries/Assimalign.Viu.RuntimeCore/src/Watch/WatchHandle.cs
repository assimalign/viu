using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The handle returned by <c>Watch</c> and <c>WatchEffect</c> — the C# port of Vue 3.5's
/// <c>WatchHandle</c> (https://vuejs.org/api/reactivity-core.html#watch). Stops the watcher,
/// or pauses and resumes callback delivery. Watchers created inside an <see cref="EffectScope"/>
/// also stop when the scope stops, so an explicit <see cref="Stop"/> is only needed for
/// independently created watchers. Implements <see cref="IDisposable"/> for <c>using</c> support.
/// </summary>
public sealed class WatchHandle : IDisposable
{
    private readonly Watcher _watcher;

    internal WatchHandle(Watcher watcher) => _watcher = watcher;

    /// <summary>Whether the watcher is still running.</summary>
    public bool IsActive => _watcher.IsActive;

    /// <summary>Stops the watcher, unlinking its dependencies and running the pending cleanup. Idempotent.</summary>
    public void Stop() => _watcher.Stop();

    /// <summary>Defers callbacks until <see cref="Resume"/>; a change while paused delivers one trailing callback.</summary>
    public void Pause() => _watcher.Pause();

    /// <summary>Resumes callback delivery paused by <see cref="Pause"/>.</summary>
    public void Resume() => _watcher.Resume();

    /// <summary>Stops the watcher; equivalent to <see cref="Stop"/> for <c>using</c> support.</summary>
    public void Dispose() => _watcher.Stop();
}
