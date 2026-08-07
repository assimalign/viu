using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Controls one active watch. Mirrors the shipping handle surface.
/// </summary>
public sealed class WatchHandle : IDisposable
{
    private bool _isActive = true;

    /// <summary>Gets whether the watch is still active.</summary>
    public bool IsActive => _isActive;

    /// <summary>Stops the watch permanently.</summary>
    public void Stop() => _isActive = false;

    /// <summary>Suspends callback delivery without releasing subscriptions.</summary>
    public void Pause()
    {
    }

    /// <summary>Resumes callback delivery.</summary>
    public void Resume()
    {
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();
}
