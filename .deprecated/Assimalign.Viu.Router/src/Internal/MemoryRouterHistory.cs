using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// The in-memory history: a queue of entries with a movable position and no browser coupling at all.
/// The C# port of vue-router's <c>createMemoryHistory</c>
/// (<c>packages/router/src/history/memory.ts</c>) — the mode used for tests and non-browser hosts,
/// and the reference model for the push/replace/go and position semantics the web history reproduces
/// over interop.
/// </summary>
/// <remarks>
/// References no interop assembly and touches no DOM. Each entry carries a full
/// <see cref="RouterHistoryState"/> (a deliberate enrichment over upstream's empty <c>{}</c> memory
/// state) so the monotonic position counter round-trips exactly as it does in the browser — the
/// [V01.01.08.02] requirement that memory reproduce the same state semantics. Not thread-safe.
/// </remarks>
internal sealed class MemoryRouterHistory : IRouterHistory
{
    private const string Start = "/";

    private readonly List<(string Location, RouterHistoryState State)> queue = [];
    private readonly List<NavigationCallback> listeners = [];
    private int position;

    internal MemoryRouterHistory(string? @base)
    {
        Base = HistoryPathNormalization.NormalizeBase(@base);
        Reset();
    }

    /// <inheritdoc/>
    public string Base { get; }

    /// <inheritdoc/>
    public string Location => queue[position].Location;

    /// <inheritdoc/>
    public RouterHistoryState State => queue[position].State;

    /// <inheritdoc/>
    public void Push(string location, RouterHistoryState? data = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        var newState = RouterHistoryStateBuilder.BuildForPush(State, location, data?.Scroll);
        SetLocation(location, newState);
    }

    /// <inheritdoc/>
    public void Replace(string location, RouterHistoryState? data = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        // Upstream: queue.splice(position--, 1); setLocation(to) — drop the current entry and
        // decrement, then setLocation re-adds at the same index (truncating any forward entries).
        var newState = RouterHistoryStateBuilder.BuildForReplace(State, location, data?.Scroll);
        queue.RemoveAt(position);
        position--;
        SetLocation(location, newState);
    }

    /// <inheritdoc/>
    public void Go(int delta, bool triggerListeners = true)
    {
        var from = Location;
        // Upstream treats delta === 0 as forward in abstract mode (0 does not reload as it would in
        // the browser), so only a strictly negative delta is "back".
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
        ArgumentNullException.ThrowIfNull(callback);
        listeners.Add(callback);
        return () => listeners.Remove(callback);
    }

    /// <inheritdoc/>
    public string CreateHref(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return HistoryPathNormalization.CreateHref(Base, location);
    }

    /// <inheritdoc/>
    public void Destroy()
    {
        listeners.Clear();
        Reset();
    }

    // Upstream setLocation: position++, then append at the tip or truncate-from-here and append.
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
}
