using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// The browser history <em>policy</em>: it owns the current location and state, composes URLs from
/// the configured base, runs the push/replace/popstate state machine, and books navigation
/// listeners — all without touching the DOM. Every environment effect is delegated to an injected
/// <see cref="IBrowserHistoryInterop"/>. The C# port of vue-router's <c>useHistoryStateNavigation</c>
/// and <c>useHistoryListeners</c> composed by <c>createWebHistory</c>
/// (<c>packages/router/src/history/html5.ts</c>). Web and hash modes share this class; they differ
/// only in the base handed to it (a hash base carries a <c>#</c>), exactly as
/// <c>createWebHashHistory</c> just forwards a hash base to <c>createWebHistory</c>.
/// </summary>
/// <remarks>
/// Because the browser edge is an injected seam, the whole policy — base prepend/strip, the state
/// round-trip, the <c>popstate</c> delta/direction computation, listener bookkeeping, and the
/// single-crossing batched reads/writes — is unit-testable with a fake interop and no browser. Not
/// thread-safe (single-threaded JS event-loop model).
/// </remarks>
internal sealed class BrowserRouterHistory : IRouterHistory
{
    private readonly IBrowserHistoryInterop interop;
    private readonly List<NavigationCallback> listeners = [];

    private string currentLocation;
    private RouterHistoryState currentState;
    // Upstream pauseState: the location a silent go() is leaving, so its popstate is swallowed.
    private string? pausedLocation;

    internal BrowserRouterHistory(IBrowserHistoryInterop interop, string normalizedBase)
    {
        this.interop = interop;
        Base = normalizedBase;

        var snapshot = interop.ReadSnapshot();
        currentLocation = HistoryPathNormalization.CreateCurrentLocation(
            Base, snapshot.Pathname, snapshot.Search, snapshot.Hash);

        if (snapshot.State is { } existingState)
        {
            currentState = existingState;
        }
        else
        {
            // Fresh navigation with no prior state: seed the current entry (upstream seeds
            // position = history.length - 1 and replaces the entry in place).
            currentState = RouterHistoryStateBuilder.BuildInitial(currentLocation, snapshot.HistoryLength - 1);
            interop.Replace(BuildUrl(currentLocation), currentState);
        }

        // One popstate listener per history instance; torn down in Destroy.
        interop.Subscribe(OnPopState);
    }

    /// <inheritdoc/>
    public string Base { get; }

    /// <inheritdoc/>
    public string Location => currentLocation;

    /// <inheritdoc/>
    public RouterHistoryState State => currentState;

    /// <inheritdoc/>
    public void Push(string location, RouterHistoryState? data = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        // Rewrite the leaving entry to point forward at `location` (the interop fills its live scroll
        // anchor), then push the new entry — both in a single interop crossing.
        var amendedCurrent = currentState with { Forward = location, Scroll = null };
        var newState = RouterHistoryStateBuilder.BuildForPush(currentState, location, data?.Scroll);
        interop.Push(BuildUrl(currentLocation), amendedCurrent, BuildUrl(location), newState);
        currentState = newState;
        currentLocation = location;
    }

    /// <inheritdoc/>
    public void Replace(string location, RouterHistoryState? data = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        var newState = RouterHistoryStateBuilder.BuildForReplace(currentState, location, data?.Scroll);
        interop.Replace(BuildUrl(location), newState);
        currentState = newState;
        currentLocation = location;
    }

    /// <inheritdoc/>
    public void Go(int delta, bool triggerListeners = true)
    {
        if (!triggerListeners)
        {
            // Swallow the popstate this go() will provoke (upstream pauseListeners()).
            pausedLocation = currentLocation;
        }
        interop.Go(delta);
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
        interop.Unsubscribe();
    }

    // The popstate handler (upstream popStateHandler): reconcile the arrived entry, honour a paused
    // silent go, compute the signed delta from the position counters, then notify listeners.
    private void OnPopState(BrowserHistorySnapshot snapshot)
    {
        var to = HistoryPathNormalization.CreateCurrentLocation(
            Base, snapshot.Pathname, snapshot.Search, snapshot.Hash);
        var from = currentLocation;
        var fromState = currentState;

        var delta = 0;
        if (snapshot.State is { } arrivedState)
        {
            currentLocation = to;
            currentState = arrivedState;
            if (pausedLocation is not null && string.Equals(pausedLocation, from, StringComparison.Ordinal))
            {
                // A silent go(delta, triggerListeners: false) — state is reconciled, listeners are not.
                pausedLocation = null;
                return;
            }
            delta = arrivedState.Position - fromState.Position;
        }
        else
        {
            // An entry with no Viu state (created before this history, or a bare hash change):
            // synthesize one in place so subsequent navigations have a position to count from.
            Replace(to);
        }

        NotifyListeners(to, from, delta);
    }

    private void NotifyListeners(string to, string from, int delta)
    {
        var direction = delta > 0
            ? NavigationDirection.Forward
            : delta < 0
                ? NavigationDirection.Back
                : NavigationDirection.Unknown;
        var information = new NavigationInformation(NavigationType.Pop, direction, delta);
        // Snapshot so a listener that unsubscribes mid-notification does not disturb iteration.
        foreach (var callback in listeners.ToArray())
        {
            callback(to, from, information);
        }
    }

    // Compose the URL written to the environment: root-relative `base + location` for web mode, or
    // the `#…` fragment slice for hash mode (upstream changeLocation's non-<base>-element branch).
    private string BuildUrl(string location)
    {
        var hashIndex = Base.IndexOf('#');
        return hashIndex >= 0 ? Base[hashIndex..] + location : Base + location;
    }
}
