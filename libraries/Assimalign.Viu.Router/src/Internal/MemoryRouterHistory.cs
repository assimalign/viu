using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// The in-memory history: a queue of entries with a movable position and no browser coupling at all.
/// The mode used for tests and non-browser hosts, and the reference model for the push/replace/go
/// and position semantics the web history reproduces over interop.
/// </summary>
/// <remarks>
/// References no interop assembly and touches no DOM. Each entry deliberately carries a full
/// <see cref="RouterHistoryState"/> rather than an empty placeholder, so the monotonic position
/// counter and the back/forward adjacency round-trip exactly as they do in the browser — the
/// [V01.01.08.02] requirement that memory reproduce the same state semantics. Not thread-safe.
/// </remarks>
internal sealed class MemoryRouterHistory : IRouterHistory
{
    private const string Start = "/";

    private readonly List<(string Location, RouterHistoryState State)> queue = [];
    private readonly List<NavigationCallback> listeners = [];
    private readonly string normalizedBase;
    private int position;
    private bool isDisposed;

    internal MemoryRouterHistory(string? @base)
    {
        normalizedBase = HistoryPathNormalization.NormalizeBase(@base);
        Reset();
    }

    /// <inheritdoc/>
    public string Base
    {
        get
        {
            ThrowIfDisposed();
            return normalizedBase;
        }
    }

    /// <inheritdoc/>
    public string Location
    {
        get
        {
            ThrowIfDisposed();
            return queue[position].Location;
        }
    }

    /// <inheritdoc/>
    public RouterHistoryState State
    {
        get
        {
            ThrowIfDisposed();
            return queue[position].State;
        }
    }

    /// <inheritdoc/>
    public void Push(string location, RouterHistoryState? data = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(location);
        var newState = RouterHistoryStateBuilder.BuildForPush(State, location, data?.Scroll);
        SetLocation(location, newState);
    }

    /// <inheritdoc/>
    public void Replace(string location, RouterHistoryState? data = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(location);
        // Drop the current entry and step back, then re-add at the same index through SetLocation,
        // which truncates any forward entries — a replace must not leave a stale forward branch.
        var newState = RouterHistoryStateBuilder.BuildForReplace(State, location, data?.Scroll);
        queue.RemoveAt(position);
        position--;
        SetLocation(location, newState);
    }

    /// <inheritdoc/>
    public void Go(int delta, bool triggerListeners = true)
    {
        ThrowIfDisposed();
        var from = Location;
        // A zero delta is treated as forward: in memory it cannot reload the way the browser would,
        // so only a strictly negative delta counts as "back".
        var direction = delta < 0 ? NavigationDirection.Back : NavigationDirection.Forward;
        position = Math.Max(0, Math.Min(position + delta, queue.Count - 1));
        if (triggerListeners)
        {
            NotifyListeners(Location, from, direction, delta);
        }
    }

    /// <inheritdoc/>
    public Action Listen(NavigationCallback callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        listeners.Add(callback);
        return () => listeners.Remove(callback);
    }

    /// <inheritdoc/>
    public string CreateHref(string location)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(location);
        return HistoryPathNormalization.CreateHref(Base, location);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        listeners.Clear();
    }

    // Advance the position, then append at the tip or truncate-from-here and append.
    private void SetLocation(string location, RouterHistoryState state)
    {
        position++;
        if (position < queue.Count)
        {
            queue.RemoveRange(position, queue.Count - position);
        }
        queue.Add((location, state));
    }

    private void NotifyListeners(string to, string from, NavigationDirection direction, int delta)
    {
        var information = new NavigationInformation(NavigationType.Pop, direction, delta);
        // Snapshot so a listener that unsubscribes mid-notification does not disturb iteration.
        foreach (var callback in listeners.ToArray())
        {
            callback(to, from, information);
        }
    }

    private void Reset()
    {
        queue.Clear();
        position = 0;
        queue.Add((Start, RouterHistoryStateBuilder.BuildInitial(Start, position: 0)));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(isDisposed, this);
}
